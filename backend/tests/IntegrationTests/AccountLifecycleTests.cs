using System.Net;
using Hangfire;
using Hangfire.States;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.IntegrationTests;

public sealed class AccountLifecycleTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    private sealed record Account(Guid Id, string Name, string Email, string Password, AccountType Type);
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private async Task<Account> RegisterAsync(AccountType type = AccountType.Customer)
    {
        var name = "life" + Guid.NewGuid().ToString("N"); var email = name + "@example.invalid"; var password = "Test-only-" + Guid.NewGuid().ToString("N");
        if (type == AccountType.Admin)
        {
            using var scope = factory.Services.CreateScope(); var user = ApplicationUser.Create(type, name, email); user.EmailConfirmed = true;
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded, "Admin fixture creation failed.");
        }
        else
        {
            using var registered = await client.PostBrowserJsonAsync($"/api/v1/identity/{(type == AccountType.Customer ? "customers" : "merchants")}/register", new { username = name, email, password, acceptTerms = true });
            Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        }
        await using var context = database.CreateContext(); var id = await context.Set<ApplicationUser>().Where(value => value.UserName == name).Select(value => value.Id).SingleAsync();
        return new(id, name, email, password, type);
    }
    private async Task LoginAsync(Account account)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = account.Type.ToString(), identifier = account.Email, password = account.Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task DeleteAsync(Account account)
    {
        await LoginAsync(account); using var response = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = account.Password, userId = Guid.NewGuid(), accountType = "Admin" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.True((await response.Content.ReadAsStringAsync()).Length == 0, "Delete response must be empty.");
    }
    private async Task RequestAsync(Account account)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/recovery/request", new { accountType = account.Type.ToString(), email = account.Email });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.True((await response.Content.ReadAsStringAsync()).Length == 0, "Recovery acknowledgement must be empty.");
    }
    private static Task<HttpResponseMessage> RecoverAsync(HttpClient browser, Account account, string code) => browser.PostBrowserJsonAsync("/api/v1/identity/recovery/verify", new { accountType = account.Type.ToString(), email = account.Email, code });
    private async Task<EmailOtpPayload> PayloadAsync(Account account, OtpPurpose purpose)
    {
        await using var context = database.CreateContext(); var rows = await context.Set<OutboxMessage>().Where(value => value.Kind == EmailOtpHandler.MessageKind && value.ProtectedPayload != "").OrderByDescending(value => value.Id).ToArrayAsync();
        var protection = factory.Services.GetRequiredService<PayloadProtection>();
        return rows.Select(value => protection.Unprotect<EmailOtpPayload>(value.Kind, value.ProtectedPayload)).First(value => value.UserId == account.Id && value.Purpose == purpose);
    }
    private async Task<AccountDeletion> DeletionAsync(Account account) { await using var context = database.CreateContext(); return await context.Set<AccountDeletion>().AsNoTracking().SingleAsync(value => value.UserId == account.Id); }
    private async Task PurgeAsync(Account account, Guid generation)
    { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AccountPurgeJob>().RunAsync(account.Id, generation, CancellationToken.None); }

    [Fact]
    public async Task Delete_failure_after_session_SQL_update_rolls_back_status_sessions_and_schedule_intent()
    {
        var account = await RegisterAsync(); await LoginAsync(account);
        var failure = new RejectDeletionSave();
        client.Dispose(); await factory.DisposeAsync();
        factory = new(database, clock, configure: services => services.AddDbContext<HawalekDbContext>(options => options.AddInterceptors(failure))); client = factory.CreateHttpsClient();
        await LoginAsync(account);
        using (var result = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = account.Password })) Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.True(failure.SawRevocationInsideTransaction);
        await using var verify = database.CreateContext(); Assert.Equal(AccountAccessStatus.Active, (await verify.Set<ApplicationUser>().SingleAsync()).AccessStatus);
        Assert.False(await verify.Set<AccountSession>().AnyAsync(value => value.RevokedAt != null)); Assert.Empty(await verify.Set<AccountDeletion>().ToArrayAsync());
        Assert.Empty(await verify.Set<AccountDeletionAudit>().ToArrayAsync()); Assert.False(await verify.Set<OutboxMessage>().AnyAsync(value => value.Kind == DeletionScheduleHandler.MessageKind));
        using var me = await client.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
    private sealed class RejectDeletionSave : SaveChangesInterceptor
    {
        public bool SawRevocationInsideTransaction { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            if (context.ChangeTracker.Entries<AccountDeletionAudit>().Any(value => value.State == EntityState.Added && value.Entity.Kind == AccountDeletionEvent.Requested))
            {
                SawRevocationInsideTransaction = await context.Set<AccountSession>().AsNoTracking().AnyAsync(value => value.RevokedAt != null, cancellationToken);
                throw new InvalidOperationException("test-only-sensitive-delete-failure");
            }
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_outbox_uses_real_Mailpit_and_suppresses_delivery_at_deletion_deadline(bool crossDeadline)
    {
        var account = await RegisterAsync(); await DeleteAsync(account); clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromMinutes(1));
        await RequestAsync(account); var payload = await PayloadAsync(account, OtpPurpose.AccountRecovery);
        if (crossDeadline) clock.Advance(TimeSpan.FromMinutes(1));
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None);
        var received = await MailpitProbe.MessagesAsync(account.Email);
        if (crossDeadline) Assert.Empty(received);
        else Assert.True(Assert.Single(received).Contains(payload.Code, StringComparison.Ordinal), "Recovery delivery did not contain its issued code.");
    }

    [Theory]
    [InlineData(AccountType.Customer)]
    [InlineData(AccountType.Merchant)]
    public async Task Soft_delete_revokes_access_refresh_and_codes_and_recovery_never_revives_sessions(AccountType type)
    {
        var account = await RegisterAsync(type); var confirmation = await PayloadAsync(account, OtpPurpose.EmailConfirmation);
        await LoginAsync(account);
        using var login = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = type.ToString(), identifier = account.Email, password = account.Password });
        var oldCookies = login.Headers.GetValues("Set-Cookie").Where(value => value.StartsWith(IdentityAuthentication.AccessCookie + "=", StringComparison.Ordinal) || value.StartsWith(IdentityAuthentication.RefreshCookie + "=", StringComparison.Ordinal)).Select(value => value.Split(';')[0]);
        var rawCookies = string.Join("; ", oldCookies);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = account.Password, userId = Guid.NewGuid() })) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var deletion = await DeletionAsync(account); Assert.Equal(clock.GetUtcNow().AddDays(30), deletion.PurgeAt);
        using (var scope = factory.Services.CreateScope()) Assert.True((await scope.ServiceProvider.GetRequiredService<IAccountLifecycle>().DeleteAsync(account.Id, account.Password, CancellationToken.None)).IsSuccess);
        Assert.Equal(deletion.PurgeAt, (await DeletionAsync(account)).PurgeAt);
        using var raw = factory.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/me")) { request.Headers.Add("Cookie", rawCookies); using var denied = await raw.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); }
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/refresh")) { await BrowserRequests.AddCsrfAsync(raw, request, rawCookies); using var denied = await raw.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); }
        await RequestAsync(account); var recovery = await PayloadAsync(account, OtpPurpose.AccountRecovery);
        using (var wrong = await RecoverAsync(client, account, confirmation.Code)) Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var other = factory.CreateHttpsClient(); var results = await Task.WhenAll(RecoverAsync(client, account, recovery.Code), RecoverAsync(other, account, recovery.Code));
        Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.NoContent)); Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.BadRequest)); foreach (var result in results) result.Dispose();
        using (var me = await client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/me")) { request.Headers.Add("Cookie", rawCookies); using var denied = await raw.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); }
        await LoginAsync(account);
        await using var context = database.CreateContext(); var user = await context.Set<ApplicationUser>().SingleAsync();
        Assert.Equal(AccountAccessStatus.Active, user.AccessStatus); Assert.False(user.EmailConfirmed);
        Assert.Equal(2, await context.Set<AccountSession>().CountAsync(value => value.UserId == account.Id && value.RevokedAt != null));
        Assert.Equal(2, await context.Set<AccountDeletionAudit>().CountAsync());
        if (type == AccountType.Merchant) Assert.Equal(MerchantApprovalStatus.Pending, (await context.Set<MerchantAccount>().SingleAsync()).Status);
    }

    [Fact]
    public async Task Admin_is_forbidden_and_delete_requires_CSRF_password_and_server_owned_identity()
    {
        var admin = await RegisterAsync(AccountType.Admin); await LoginAsync(admin);
        using (var denied = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = admin.Password })) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await RequestAsync(admin);
        var account = await RegisterAsync(); await LoginAsync(account);
        using (var missing = await client.DeleteAsync("/api/v1/identity/account")) Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var wrong = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = "Wrong-test-only-password" })) Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using (var success = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = account.Password, userId = admin.Id })) Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
        await using var context = database.CreateContext(); Assert.Equal(AccountAccessStatus.Active, (await context.Set<ApplicationUser>().SingleAsync(value => value.Id == admin.Id)).AccessStatus);
        Assert.Equal(account.Id, (await context.Set<AccountDeletion>().SingleAsync()).UserId); Assert.False(await context.Set<OtpChallenge>().AnyAsync(value => value.UserId == admin.Id && value.Purpose == OtpPurpose.AccountRecovery));
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("attempts")]
    [InlineData("deadline")]
    public async Task Recovery_checks_OTP_and_exact_deletion_deadline_under_the_SQL_lock(string reason)
    {
        var account = await RegisterAsync(); await DeleteAsync(account);
        if (reason == "deadline") clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromMinutes(1));
        await RequestAsync(account); var recovery = await PayloadAsync(account, OtpPurpose.AccountRecovery);
        if (reason == "expiry") clock.Advance(TimeSpan.FromMinutes(10));
        else if (reason == "deadline") clock.Advance(TimeSpan.FromMinutes(1));
        else
        {
            var wrong = recovery.Code == "000000" ? "111111" : "000000";
            for (var index = 0; index < 5; index++) { using var rejected = await RecoverAsync(client, account, wrong); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode); }
        }
        using (var rejected = await RecoverAsync(client, account, recovery.Code)) Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var context = database.CreateContext(); Assert.Equal(AccountAccessStatus.Deleted, (await context.Set<ApplicationUser>().SingleAsync()).AccessStatus);
        var challenge = await context.Set<OtpChallenge>().SingleAsync(value => value.Purpose == OtpPurpose.AccountRecovery);
        Assert.Null(challenge.ConsumedAt); if (reason == "attempts") Assert.NotNull(challenge.LockedUntil); if (reason == "deadline") Assert.True(challenge.ExpiresAt > clock.GetUtcNow());
    }

    [Theory]
    [InlineData(AccountType.Customer)]
    [InlineData(AccountType.Merchant)]
    public async Task Duplicate_purges_remove_only_owned_P02_data_and_reserve_both_identifiers_forever_inside_type(AccountType type)
    {
        var account = await RegisterAsync(type); var other = await RegisterAsync(); await DeleteAsync(account); var deletion = await DeletionAsync(account);
        await using (var context = database.CreateContext()) await context.Set<OutboxMessage>().Where(value => value.SubjectId == account.Id && value.Kind == EmailOtpHandler.MessageKind).ExecuteUpdateAsync(setters => setters.SetProperty(value => value.SubjectId, (Guid?)null));
        clock.Advance(TimeSpan.FromDays(30)); await Task.WhenAll(PurgeAsync(account, deletion.Generation), PurgeAsync(account, deletion.Generation));
        await using (var context = database.CreateContext())
        {
            Assert.False(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id)); Assert.True(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == other.Id));
            Assert.False(await context.Set<AccountSession>().AnyAsync(value => value.UserId == account.Id)); Assert.False(await context.Set<OtpChallenge>().AnyAsync(value => value.UserId == account.Id));
            Assert.False(await context.Set<MerchantAccount>().AnyAsync(value => value.UserId == account.Id)); Assert.False(await context.Set<AccountDeletion>().AnyAsync(value => value.UserId == account.Id));
            Assert.Equal(2, await context.Set<ReservedIdentifier>().CountAsync(value => value.AccountType == type));
            Assert.Equal(1, await context.Set<AccountDeletionAudit>().CountAsync(value => value.UserId == account.Id && value.Kind == AccountDeletionEvent.Purged));
            Assert.False(await context.Set<OutboxMessage>().AnyAsync(value => value.SubjectId == account.Id && value.ProtectedPayload != ""));
            Assert.True(await context.Set<OutboxMessage>().AnyAsync(value => value.SubjectId == other.Id && value.ProtectedPayload != ""));
        }
        var body = new { username = account.Name, email = account.Email, password = account.Password, acceptTerms = true };
        using (var reserved = await client.PostBrowserJsonAsync($"/api/v1/identity/{(type == AccountType.Customer ? "customers" : "merchants")}/register", body)) Assert.Equal(HttpStatusCode.NoContent, reserved.StatusCode);
        using (var separateType = await client.PostBrowserJsonAsync($"/api/v1/identity/{(type == AccountType.Customer ? "merchants" : "customers")}/register", body)) Assert.Equal(HttpStatusCode.NoContent, separateType.StatusCode);
        await using var verify = database.CreateContext(); var recreated = await verify.Set<ApplicationUser>().SingleAsync(value => value.UserName == account.Name);
        Assert.NotEqual(type, recreated.AccountType); Assert.NotEqual(account.Id, recreated.Id);
    }

    [Fact]
    public async Task Recovered_and_redeleted_accounts_are_safe_from_old_generation_jobs()
    {
        var account = await RegisterAsync(); await DeleteAsync(account); var old = await DeletionAsync(account);
        await RequestAsync(account); var recovery = await PayloadAsync(account, OtpPurpose.AccountRecovery);
        using (var accepted = await RecoverAsync(client, account, recovery.Code)) Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        clock.Advance(TimeSpan.FromDays(1)); await DeleteAsync(account); var current = await DeletionAsync(account); Assert.NotEqual(old.Generation, current.Generation);
        clock.Advance(TimeSpan.FromDays(29)); await PurgeAsync(account, old.Generation); await PurgeAsync(account, current.Generation);
        await using (var context = database.CreateContext()) Assert.True(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
        clock.Advance(TimeSpan.FromDays(1)); await PurgeAsync(account, current.Generation);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
    }

    [Fact]
    public async Task Recovery_and_purge_contend_on_real_SQL_lock_and_recheck_time_after_waiting()
    {
        var account = await RegisterAsync(); await DeleteAsync(account); var deletion = await DeletionAsync(account);
        clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromMinutes(1)); await RequestAsync(account); var payload = await PayloadAsync(account, OtpPurpose.AccountRecovery);
        using var lockScope = factory.Services.CreateScope(); var lockedContext = lockScope.ServiceProvider.GetRequiredService<HawalekDbContext>();
        await using var transaction = await lockedContext.BeginAsync(); await lockScope.ServiceProvider.GetRequiredService<IdentityStateLock>().AcquireAsync(account.Id, CancellationToken.None);
        var recovery = RecoverAsync(client, account, payload.Code); var purge = PurgeAsync(account, deletion.Generation);
        await Task.Delay(100); Assert.False(recovery.IsCompleted); Assert.False(purge.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(1)); await transaction.CommitAsync();
        using var result = await recovery; await purge; Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
        Assert.False(await verify.Set<AccountDeletionAudit>().AnyAsync(value => value.Kind == AccountDeletionEvent.Recovered));
    }

    [Theory]
    [InlineData("identifier-key")]
    [InlineData("outbox-key")]
    public async Task Wrong_durable_key_fails_closed_without_partial_purge_or_reservation_bypass(string setting)
    {
        var account = await RegisterAsync(); await DeleteAsync(account); var deletion = await DeletionAsync(account); clock.Advance(TimeSpan.FromDays(30));
        if (setting == "outbox-key")
        { await using var context = database.CreateContext(); await context.Set<OutboxMessage>().Where(value => value.SubjectId == account.Id && value.Kind == EmailOtpHandler.MessageKind).ExecuteUpdateAsync(setters => setters.SetProperty(value => value.SubjectId, (Guid?)null)); }
        client.Dispose(); await factory.DisposeAsync(); factory = new(database, clock, settings: new Dictionary<string, string?> { [setting == "identifier-key" ? "Identity:IdentifierKey" : "Outbox:EncryptionKey"] = TestIdentityConfiguration.NewKey() }); client = factory.CreateHttpsClient();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PurgeAsync(account, deletion.Generation)); Assert.Equal("Account purge failed.", error.Message); Assert.Null(error.InnerException);
        await using (var context = database.CreateContext()) { Assert.True(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id)); Assert.Empty(await context.Set<ReservedIdentifier>().ToArrayAsync()); }
        if (setting == "identifier-key")
        {
            using var rejected = await client.PostBrowserJsonAsync("/api/v1/identity/customers/register", new { username = "rotation" + Guid.NewGuid().ToString("N"), email = "rotation" + Guid.NewGuid().ToString("N") + "@example.invalid", password = account.Password, acceptTerms = true });
            Assert.Equal(HttpStatusCode.InternalServerError, rejected.StatusCode);
        }
        client.Dispose(); await factory.DisposeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); await PurgeAsync(account, deletion.Generation);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
    }

    [Fact]
    public async Task Failure_after_real_merchant_delete_rolls_back_payloads_profile_and_user_then_retry_succeeds()
    {
        var account = await RegisterAsync(AccountType.Merchant); await DeleteAsync(account); var deletion = await DeletionAsync(account); clock.Advance(TimeSpan.FromDays(30));
        client.Dispose(); await factory.DisposeAsync(); var observation = new PurgeObservation();
        factory = new(database, clock, configure: services => { services.AddSingleton(observation); services.Replace(ServiceDescriptor.Scoped<IMerchantAccounts, FailingPurge>()); }); client = factory.CreateHttpsClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PurgeAsync(account, deletion.Generation)); Assert.True(observation.DeletedInsideTransaction);
        await using (var context = database.CreateContext())
        {
            Assert.True(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id)); Assert.True(await context.Set<MerchantAccount>().AnyAsync(value => value.UserId == account.Id));
            Assert.True(await context.Set<OutboxMessage>().AnyAsync(value => value.SubjectId == account.Id && value.ProtectedPayload != "")); Assert.Empty(await context.Set<ReservedIdentifier>().ToArrayAsync());
        }
        client.Dispose(); await factory.DisposeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); await PurgeAsync(account, deletion.Generation);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
    }
    private sealed class PurgeObservation { public bool DeletedInsideTransaction { get; set; } }
    private sealed class FailingPurge(HawalekDbContext context, PurgeObservation observation) : IMerchantAccounts
    {
        public Task CreatePendingAsync(Guid userId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unused test operation.");
        public Task<MerchantAccountInfo?> FindByOwnerAsync(Guid userId, CancellationToken cancellationToken = default) => new MerchantAccounts(context).FindByOwnerAsync(userId, cancellationToken);
        public async Task RemoveIdentityProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await new MerchantAccounts(context).RemoveIdentityProfileAsync(userId, cancellationToken);
            observation.DeletedInsideTransaction = !await context.Set<MerchantAccount>().AnyAsync(value => value.UserId == userId, cancellationToken);
            throw new InvalidOperationException("test-only-sensitive-purge-failure");
        }
    }

    [Fact]
    public async Task Scheduled_SQL_job_survives_host_restart_and_duplicate_worker_execution_is_harmless()
    {
        var account = await RegisterAsync(); await DeleteAsync(account); var deletion = await DeletionAsync(account);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None);
        deletion = await DeletionAsync(account); Assert.NotNull(deletion.ScheduledJobId);
        var storage = factory.Services.GetRequiredService<JobStorage>();
        using (var connection = storage.GetConnection())
        {
            var stored = connection.GetJobData(deletion.ScheduledJobId); Assert.Equal("Scheduled", stored.State);
            Assert.Equal(account.Id, Assert.IsType<Guid>(stored.Job.Args[0])); Assert.Equal(deletion.Generation, Assert.IsType<Guid>(stored.Job.Args[1])); Assert.Equal(3, stored.Job.Args.Count);
        }
        await using (var context = database.CreateContext())
        {
            var args = await context.Database.SqlQueryRaw<string>("SELECT [Arguments] AS [Value] FROM [hangfire].[Job]").ToArrayAsync();
            Assert.All(args, value => Assert.False(value.Contains(account.Email, StringComparison.Ordinal) || value.Contains(account.Password, StringComparison.Ordinal), "Sensitive value leaked into job arguments."));
        }
        client.Dispose(); await factory.DisposeAsync(); clock.Advance(TimeSpan.FromDays(30));
        factory = new(database, clock, settings: new Dictionary<string, string?> { ["Background:Enabled"] = "true" }); client = factory.CreateHttpsClient();
        var jobs = factory.Services.GetRequiredService<IBackgroundJobClient>();
        Assert.True(jobs.ChangeState(deletion.ScheduledJobId, new EnqueuedState("identity"), "Scheduled")); // Controlled clock, no wait of30 real days.
        var duplicate = jobs.Enqueue<AccountPurgeJob>(job => job.RunAsync(account.Id, deletion.Generation, CancellationToken.None));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        storage = factory.Services.GetRequiredService<JobStorage>();
        while (true)
        {
            using var connection = storage.GetConnection();
            if (connection.GetJobData(deletion.ScheduledJobId)?.State == "Succeeded" && connection.GetJobData(duplicate)?.State == "Succeeded") break;
            await Task.Delay(100, timeout.Token);
        }
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<ApplicationUser>().AnyAsync(value => value.Id == account.Id));
        Assert.Equal(2, await verify.Set<ReservedIdentifier>().CountAsync()); Assert.Equal(1, await verify.Set<AccountDeletionAudit>().CountAsync(value => value.Kind == AccountDeletionEvent.Purged));
    }
}
