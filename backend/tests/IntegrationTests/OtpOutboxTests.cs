using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Hangfire;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// A distinct SQL database for each test: pumps cannot consume another test's work.
public sealed class OtpOutboxTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    private readonly string name = "otp" + Guid.NewGuid().ToString("N");
    private string Email => name + "@example.invalid";
    public async Task InitializeAsync()
    {
        await database.InitializeAsync();
        factory = new(database, clock); client = factory.CreateHttpsClient();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private async Task RegisterAsync(string type = "customers")
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/identity/{type}/register", new
        { username = name, email = Email, password = "Test-only-" + Guid.NewGuid().ToString("N"), acceptTerms = true });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task<EmailOtpPayload> PayloadAsync()
    {
        await using var context = database.CreateContext();
        var message = await context.Set<OutboxMessage>().OrderByDescending(value => value.CreatedAt).ThenByDescending(value => value.Id).FirstAsync();
        return factory.Services.GetRequiredService<PayloadProtection>().Unprotect<EmailOtpPayload>(EmailOtpHandler.MessageKind, message.ProtectedPayload);
    }
    private async Task<HttpResponseMessage> VerifyAsync(string code, string type = "Customer", string? email = null) =>
        await client.PostAsJsonAsync("/api/v1/identity/email/verify", new { accountType = type, email = email ?? Email, code });
    private async Task RequestAsync(string type = "Customer", string? email = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/identity/email/request", new { accountType = type, email = email ?? Email });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task PumpAsync()
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Concurrent_HTTP_verification_consumes_once_and_never_approves_the_merchant()
    {
        await RegisterAsync("merchants"); var payload = await PayloadAsync();
        using (var wrongType = await VerifyAsync(payload.Code)) Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        using (var wrongOwner = await VerifyAsync(payload.Code, "Merchant", "other-" + Email)) Assert.Equal(HttpStatusCode.BadRequest, wrongOwner.StatusCode);
        var responses = await Task.WhenAll(VerifyAsync(payload.Code, "Merchant"), VerifyAsync(payload.Code, "Merchant"));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        foreach (var response in responses)
        {
            Assert.False((await response.Content.ReadAsStringAsync()).Contains(payload.Code, StringComparison.Ordinal), "OTP appeared in a response.");
            Assert.False(response.Headers.Contains("Set-Cookie")); response.Dispose();
        }
        using (var replay = await VerifyAsync(payload.Code, "Merchant")) Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        await using var context = database.CreateContext();
        Assert.True((await context.Set<ApplicationUser>().SingleAsync()).EmailConfirmed);
        Assert.Equal(MerchantApprovalStatus.Pending, (await context.Set<MerchantAccount>().SingleAsync()).Status);
        Assert.NotNull((await context.Set<OtpChallenge>().SingleAsync()).ConsumedAt);
        await PumpAsync();
        Assert.Equal(OutboxState.Suppressed, (await context.Set<OutboxMessage>().AsNoTracking().SingleAsync()).State);
    }

    [Fact]
    public async Task Resend_retains_failed_attempts_and_rate_limits_but_expired_and_unknown_requests_are_generic()
    {
        await RegisterAsync(); var first = await PayloadAsync();
        var wrong = first.Code == "000000" ? "111111" : "000000";
        for (var count = 0; count < 4; count++)
        { using var response = await VerifyAsync(wrong); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        await RequestAsync();
        await using (var context = database.CreateContext()) Assert.Equal(1, await context.Set<OutboxMessage>().CountAsync());
        clock.Advance(TimeSpan.FromSeconds(60)); await RequestAsync(); var second = await PayloadAsync();
        Assert.NotEqual(first.Generation, second.Generation);
        await using (var context = database.CreateContext()) Assert.Equal(4, (await context.Set<OtpChallenge>().SingleAsync()).FailedAttempts);
        var wrongSecond = second.Code == "000000" ? "111111" : "000000";
        using (var response = await VerifyAsync(wrongSecond)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(60)); await RequestAsync();
        using (var response = await VerifyAsync(second.Code)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using (var context = database.CreateContext())
        { Assert.Equal(2, await context.Set<OutboxMessage>().CountAsync()); Assert.NotNull((await context.Set<OtpChallenge>().SingleAsync()).LockedUntil); }
        clock.Advance(TimeSpan.FromMinutes(30)); await RequestAsync(); var unlocked = await PayloadAsync();
        clock.Advance(TimeSpan.FromMinutes(10));
        using (var expired = await VerifyAsync(unlocked.Code)) Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        await RequestAsync(email: "unknown-" + Email);
        await using (var context = database.CreateContext())
        { Assert.Equal(3, await context.Set<OutboxMessage>().CountAsync()); Assert.False((await context.Set<ApplicationUser>().SingleAsync()).EmailConfirmed); }
    }

    [Fact]
    public async Task Purpose_and_generation_are_bound_to_the_hash_and_authenticated_encrypted_payload()
    {
        await RegisterAsync(); var payload = await PayloadAsync();
        using var scope = factory.Services.CreateScope();
        var otp = scope.ServiceProvider.GetRequiredService<OtpProtection>();
        var protection = scope.ServiceProvider.GetRequiredService<PayloadProtection>();
        await using var context = database.CreateContext();
        var challenge = await context.Set<OtpChallenge>().SingleAsync();
        Assert.True(otp.Matches(challenge, payload.Code), "Current code should match without exposing it.");
        Assert.False(otp.Hash(payload.UserId, OtpPurpose.PasswordReset, payload.Generation, payload.Code) == challenge.Digest);
        Assert.False(otp.Hash(payload.UserId, payload.Purpose, Guid.NewGuid(), payload.Code) == challenge.Digest);
        var message = await context.Set<OutboxMessage>().SingleAsync();
        Assert.False(message.ProtectedPayload.Contains(Email, StringComparison.Ordinal), "Plaintext recipient persisted.");
        Assert.False(message.ProtectedPayload.Contains("\"Code\"", StringComparison.Ordinal), "Plaintext OTP payload persisted.");
        Assert.Throws<AuthenticationTagMismatchException>(() => protection.Unprotect<EmailOtpPayload>("other-kind", message.ProtectedPayload));
        var bytes = Convert.FromBase64String(message.ProtectedPayload[3..]); bytes[^1] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => protection.Unprotect<EmailOtpPayload>(message.Kind, "v1." + Convert.ToBase64String(bytes)));
        var changedKey = new PayloadProtection(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Outbox:EncryptionKey"] = TestIdentityConfiguration.NewKey() }).Build());
        Assert.Throws<AuthenticationTagMismatchException>(() => changedKey.Unprotect<EmailOtpPayload>(message.Kind, message.ProtectedPayload));
    }

    [Fact]
    public async Task User_challenge_and_email_intent_rollback_together_after_actual_SQL_saves()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HawalekDbContext>();
            await using var transaction = await context.BeginAsync();
            var user = ApplicationUser.Create(AccountType.Customer, name, Email);
            var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Test-only-" + Guid.NewGuid().ToString("N"));
            Assert.True(result.Succeeded);
            Assert.True(await scope.ServiceProvider.GetRequiredService<EmailConfirmationService>().IssueInsideTransactionAsync(user, OtpPurpose.EmailConfirmation, CancellationToken.None));
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.Set<OutboxMessage>().CountAsync());
            // Dispose without Commit: real SQL rollback, even though UserManager and SaveChanges executed.
        }
        await using var verify = database.CreateContext();
        Assert.Empty(await verify.Set<ApplicationUser>().ToArrayAsync());
        Assert.Empty(await verify.Set<OtpChallenge>().ToArrayAsync());
        Assert.Empty(await verify.Set<OutboxMessage>().ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_pumps_claim_once_and_completed_messages_are_not_resent_to_real_Mailpit()
    {
        await RegisterAsync(); var payload = await PayloadAsync();
        await Task.WhenAll(PumpAsync(), PumpAsync()); await PumpAsync();
        var texts = await MailpitProbe.MessagesAsync(Email);
        Assert.True(Assert.Single(texts).Contains(payload.Code, StringComparison.Ordinal), "Mailpit did not receive the issued code.");
        await using var context = database.CreateContext();
        var message = await context.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(OutboxState.Sent, message.State); Assert.Equal(1, message.Attempts); Assert.Equal("", message.ProtectedPayload);
    }

    [Fact]
    public async Task Expired_claim_is_recovered_after_host_restart_by_actual_Hangfire_SQL_worker()
    {
        await RegisterAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var claim = await scope.ServiceProvider.GetRequiredService<OutboxPump>().ClaimAsync(CancellationToken.None);
            Assert.NotNull(claim); Assert.Equal(OutboxState.Processing, claim.State);
        }
        client.Dispose(); await factory.DisposeAsync();
        clock.Advance(TimeSpan.FromMinutes(3));
        var settings = new Dictionary<string, string?> { ["Background:Enabled"] = "true" };
        factory = new(database, clock, settings: settings); client = factory.CreateHttpsClient();
        await WaitUntilAsync(async () =>
        {
            await using var context = database.CreateContext();
            return await context.Set<OutboxMessage>().AnyAsync(value => value.State == OutboxState.Sent);
        });
        var storage = factory.Services.GetRequiredService<JobStorage>();
        await WaitUntilAsync(() => Task.FromResult(storage.GetMonitoringApi().GetStatistics().Succeeded > 0));
        using (var connection = storage.GetConnection())
        {
            var recurring = connection.GetAllItemsFromSet("recurring-jobs"); Assert.Contains("outbox-v1", recurring);
        }
        await using (var context = database.CreateContext())
        {
            var jobs = await context.Database.SqlQueryRaw<string>("SELECT [Arguments] AS [Value] FROM [hangfire].[Job]").ToArrayAsync();
            Assert.NotEmpty(jobs); Assert.All(jobs, args => Assert.False(args.Contains(Email, StringComparison.Ordinal), "Recipient leaked into job arguments."));
            Assert.Equal(2, (await context.Set<OutboxMessage>().SingleAsync()).Attempts);
        }
        using (var dashboard = await client.GetAsync("/hangfire")) Assert.Equal(HttpStatusCode.NotFound, dashboard.StatusCode);
        client.Dispose(); await factory.DisposeAsync();
        factory = new(database, clock, settings: settings); client = factory.CreateHttpsClient();
        await WaitUntilAsync(() => Task.FromResult(factory.Services.GetRequiredService<JobStorage>().GetMonitoringApi().GetStatistics().Succeeded >= 2));
        Assert.Single(await MailpitProbe.MessagesAsync(Email));
        var worker = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<HangfireWorker>().Single();
        await Task.WhenAll(Task.Run(() => worker.StopAsync(CancellationToken.None)), Task.Run(worker.Dispose));
        await worker.StopAsync(CancellationToken.None); // host disposal will safely repeat this too
    }

    [Fact]
    public async Task Provider_acceptance_with_lost_ACK_retries_and_demonstrates_possible_duplicate_mail()
    {
        await using var proxy = new AckLossSmtpProxy();
        client.Dispose(); await factory.DisposeAsync();
        factory = new(database, clock, settings: new Dictionary<string, string?> { ["Email:SmtpPort"] = proxy.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        client = factory.CreateHttpsClient();
        await RegisterAsync(); var payload = await PayloadAsync();
        await PumpAsync();
        await using (var context = database.CreateContext())
        {
            var message = await context.Set<OutboxMessage>().SingleAsync();
            Assert.Equal(OutboxState.Pending, message.State); Assert.Equal("delivery.failed", message.LastFailureCode); Assert.Equal(1, message.Attempts);
        }
        Assert.Single(await MailpitProbe.MessagesAsync(Email));
        clock.Advance(TimeSpan.FromSeconds(11)); await PumpAsync(); await PumpAsync();
        var received = await MailpitProbe.MessagesAsync(Email);
        Assert.Equal(2, received.Length); Assert.All(received, text => Assert.True(text.Contains(payload.Code, StringComparison.Ordinal), "Retry changed the issued challenge."));
        await using var verify = database.CreateContext();
        var completed = await verify.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(OutboxState.Sent, completed.State); Assert.Equal(2, completed.Attempts); Assert.Null(completed.LastFailureCode);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }
}
