using System.Net;
using System.Net.Http.Json;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.IntegrationTests;

public sealed class AccountSecurityTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    private sealed record Account(Guid Id, string Name, string Email, string Password, AccountType Type);
    private static string Password() => "Test-only-" + Guid.NewGuid().ToString("N");
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private async Task<Account> RegisterAsync(AccountType type = AccountType.Customer)
    {
        var name = "security" + Guid.NewGuid().ToString("N"); var email = name + "@example.invalid"; var password = Password();
        if (type == AccountType.Admin)
        {
            using var scope = factory.Services.CreateScope();
            var user = ApplicationUser.Create(type, name, email); user.EmailConfirmed = true;
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded, "Admin fixture creation failed.");
        }
        else
        {
            using var response = await client.PostBrowserJsonAsync($"/api/v1/identity/{(type == AccountType.Customer ? "customers" : "merchants")}/register", new { username = name, email, password, acceptTerms = true });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        await using var context = database.CreateContext(); var id = await context.Set<ApplicationUser>().Where(user => user.UserName == name).Select(user => user.Id).SingleAsync();
        return new(id, name, email, password, type);
    }
    private static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, Account account, string? password = null) =>
        await browser.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = account.Type.ToString(), identifier = account.Email, password = password ?? account.Password });
    private static async Task RequestAsync(HttpClient browser, Account account)
    {
        using var response = await browser.PostBrowserJsonAsync("/api/v1/identity/password/request", new { accountType = account.Type.ToString(), email = account.Email });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); Assert.True((await response.Content.ReadAsStringAsync()).Length == 0, "Recovery request body must be empty.");
    }
    private static Task<HttpResponseMessage> ResetAsync(HttpClient browser, Account account, string code, string password) =>
        browser.PostBrowserJsonAsync("/api/v1/identity/password/reset", new { accountType = account.Type.ToString(), email = account.Email, code, newPassword = password });
    private async Task<EmailOtpPayload> PayloadAsync(Account account, OtpPurpose purpose)
    {
        await using var context = database.CreateContext(); var messages = await context.Set<OutboxMessage>().Where(value => value.ProtectedPayload != "").OrderByDescending(value => value.Id).ToArrayAsync();
        var protection = factory.Services.GetRequiredService<PayloadProtection>();
        return messages.Select(message => protection.Unprotect<EmailOtpPayload>(message.Kind, message.ProtectedPayload))
            .First(payload => payload.UserId == account.Id && payload.Purpose == purpose);
    }
    private static string Cookie(HttpResponseMessage response, string name) => response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(name + "=", StringComparison.Ordinal)).Split(';')[0][(name.Length + 1)..];
    private async Task<HttpResponseMessage> RawAsync(string route, string cookieName, string value, bool post)
    {
        using var raw = factory.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, route);
        if (post) await BrowserRequests.AddCsrfAsync(raw, request, cookieName + "=" + value); else request.Headers.Add("Cookie", cookieName + "=" + value);
        return await raw.SendAsync(request);
    }
    private async Task PumpAsync() { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None); }

    [Theory]
    [InlineData(AccountType.Customer)]
    [InlineData(AccountType.Merchant)]
    [InlineData(AccountType.Admin)]
    public async Task Reset_changes_password_once_and_revokes_every_previous_SQL_session_without_confirming_email(AccountType type)
    {
        var account = await RegisterAsync(type);
        using var first = await LoginAsync(client, account); Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var access = Cookie(first, IdentityAuthentication.AccessCookie); var refresh = Cookie(first, IdentityAuthentication.RefreshCookie);
        using var secondClient = factory.CreateHttpsClient(); using (var second = await LoginAsync(secondClient, account)) Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        await RequestAsync(client, account); var payload = await PayloadAsync(account, OtpPurpose.PasswordReset); var nextPassword = Password();
        using (var reset = await ResetAsync(client, account, payload.Code, nextPassword))
        { Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode); Assert.True((await reset.Content.ReadAsStringAsync()).Length == 0, "Reset body must be empty."); }
        using (var previousAccess = await RawAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, access, false)) Assert.Equal(HttpStatusCode.Unauthorized, previousAccess.StatusCode);
        using (var previousRefresh = await RawAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh, true)) Assert.Equal(HttpStatusCode.Unauthorized, previousRefresh.StatusCode);
        using (var second = await secondClient.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        using (var replay = await ResetAsync(client, account, payload.Code, Password())) Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using (var oldPassword = await LoginAsync(client, account)) Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        using (var newPassword = await LoginAsync(client, account, nextPassword)) Assert.Equal(HttpStatusCode.NoContent, newPassword.StatusCode);
        await using var context = database.CreateContext();
        Assert.Equal(2, await context.Set<AccountSession>().CountAsync(value => value.UserId == account.Id && value.RevokedAt != null));
        Assert.Equal(type == AccountType.Admin, (await context.Set<ApplicationUser>().SingleAsync()).EmailConfirmed);
        Assert.NotNull((await context.Set<OtpChallenge>().SingleAsync(value => value.Purpose == OtpPurpose.PasswordReset)).ConsumedAt);
    }

    [Fact]
    public async Task Concurrent_reset_consumes_one_challenge_and_only_one_new_password_wins()
    {
        var account = await RegisterAsync(); await RequestAsync(client, account); var payload = await PayloadAsync(account, OtpPurpose.PasswordReset);
        var firstPassword = Password(); var secondPassword = Password(); using var other = factory.CreateHttpsClient();
        var results = await Task.WhenAll(ResetAsync(client, account, payload.Code, firstPassword), ResetAsync(other, account, payload.Code, secondPassword));
        Assert.Equal(1, results.Count(result => result.StatusCode == HttpStatusCode.NoContent)); Assert.Equal(1, results.Count(result => result.StatusCode == HttpStatusCode.BadRequest));
        var winner = results[0].StatusCode == HttpStatusCode.NoContent ? firstPassword : secondPassword;
        var loser = results[0].StatusCode == HttpStatusCode.NoContent ? secondPassword : firstPassword;
        foreach (var result in results) result.Dispose();
        using (var rejected = await LoginAsync(client, account, loser)) Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var accepted = await LoginAsync(client, account, winner); Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task Confirmation_and_reset_codes_cannot_cross_purposes_and_weak_password_does_not_consume_reset()
    {
        var account = await RegisterAsync(); var confirmation = await PayloadAsync(account, OtpPurpose.EmailConfirmation);
        await RequestAsync(client, account); var reset = await PayloadAsync(account, OtpPurpose.PasswordReset);
        Assert.False(confirmation.Code == reset.Code, "Latest purpose codes collided.");
        using (var wrongPurpose = await ResetAsync(client, account, confirmation.Code, Password())) Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        using (var wrongPurpose = await client.PostBrowserJsonAsync("/api/v1/identity/email/verify", new { accountType = "Customer", email = account.Email, code = reset.Code })) Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        using (var weak = await ResetAsync(client, account, reset.Code, "short-test")) Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        await using (var context = database.CreateContext()) Assert.Null((await context.Set<OtpChallenge>().SingleAsync(value => value.Purpose == OtpPurpose.PasswordReset)).ConsumedAt);
        using var success = await ResetAsync(client, account, reset.Code, Password()); Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("attempts")]
    public async Task Reset_expiry_and_failed_attempt_lockout_are_persisted_without_password_change(string mode)
    {
        var account = await RegisterAsync(); await RequestAsync(client, account); var payload = await PayloadAsync(account, OtpPurpose.PasswordReset);
        if (mode == "expiry") clock.Advance(TimeSpan.FromMinutes(10));
        else
        {
            var wrong = payload.Code == "000000" ? "111111" : "000000";
            for (var count = 0; count < 5; count++) { using var failure = await ResetAsync(client, account, wrong, Password()); Assert.Equal(HttpStatusCode.BadRequest, failure.StatusCode); }
            clock.Advance(TimeSpan.FromSeconds(60)); await RequestAsync(client, account);
        }
        using (var denied = await ResetAsync(client, account, payload.Code, Password())) Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using (var original = await LoginAsync(client, account)) Assert.Equal(HttpStatusCode.NoContent, original.StatusCode);
        await using var context = database.CreateContext(); var challenge = await context.Set<OtpChallenge>().SingleAsync(value => value.Purpose == OtpPurpose.PasswordReset);
        Assert.Null(challenge.ConsumedAt); if (mode == "attempts") { Assert.Equal(5, challenge.FailedAttempts); Assert.NotNull(challenge.LockedUntil); }
    }

    [Fact]
    public async Task Unknown_or_inactive_reset_request_is_identical_and_enqueues_no_reset_message()
    {
        var account = await RegisterAsync();
        await using (var context = database.CreateContext()) { var user = await context.Set<ApplicationUser>().SingleAsync(); context.Entry(user).Property(value => value.AccessStatus).CurrentValue = AccountAccessStatus.Suspended; await context.SaveChangesAsync(); }
        using (var unknown = await client.PostBrowserJsonAsync("/api/v1/identity/password/request", new { accountType = "Customer", email = "unknown-" + account.Email }))
        { Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode); Assert.True((await unknown.Content.ReadAsStringAsync()).Length == 0, "Unknown recovery body must be empty."); }
        await RequestAsync(client, account);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<OtpChallenge>().AnyAsync(value => value.Purpose == OtpPurpose.PasswordReset));
        Assert.Single(await verify.Set<OutboxMessage>().ToArrayAsync());
    }

    private sealed class RemovalObservation { public bool NullHash { get; set; } public bool ConsumedChallenge { get; set; } }
    private sealed class RejectAfterRemoval(HawalekDbContext context, RemovalObservation observation) : IPasswordValidator<ApplicationUser>
    {
        public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            observation.NullHash = await context.Set<ApplicationUser>().AsNoTracking().AnyAsync(value => value.Id == user.Id && value.PasswordHash == null);
            observation.ConsumedChallenge = await context.Set<OtpChallenge>().AnyAsync(value => value.UserId == user.Id && value.Purpose == OtpPurpose.PasswordReset && value.ConsumedAt != null);
            return IdentityResult.Failed(new IdentityError { Code = "test", Description = "test-only-failure-secret" });
        }
    }
    [Fact]
    public async Task Failure_after_actual_password_removal_rolls_back_hash_stamp_challenge_and_sessions()
    {
        var account = await RegisterAsync(); using var login = await LoginAsync(client, account); var access = Cookie(login, IdentityAuthentication.AccessCookie);
        await RequestAsync(client, account); var payload = await PayloadAsync(account, OtpPurpose.PasswordReset);
        string? hash; string? stamp;
        await using (var context = database.CreateContext()) { var user = await context.Set<ApplicationUser>().SingleAsync(); hash = user.PasswordHash; stamp = user.SecurityStamp; }
        client.Dispose(); await factory.DisposeAsync(); var observation = new RemovalObservation();
        factory = new(database, clock, configure: services => { services.AddSingleton(observation); services.RemoveAll<IPasswordValidator<ApplicationUser>>(); services.AddScoped<IPasswordValidator<ApplicationUser>, RejectAfterRemoval>(); });
        client = factory.CreateHttpsClient();
        using (var failed = await ResetAsync(client, account, payload.Code, Password()))
        { Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode); Assert.False((await failed.Content.ReadAsStringAsync()).Contains("test-only-failure-secret", StringComparison.Ordinal)); }
        Assert.True(observation.NullHash && observation.ConsumedChallenge, "The failure did not occur after real Identity store saves.");
        await using (var verify = database.CreateContext())
        {
            var user = await verify.Set<ApplicationUser>().SingleAsync(); Assert.True(user.PasswordHash == hash && user.SecurityStamp == stamp, "Password state did not roll back.");
            Assert.Null((await verify.Set<OtpChallenge>().SingleAsync(value => value.Purpose == OtpPurpose.PasswordReset)).ConsumedAt);
            Assert.Null((await verify.Set<AccountSession>().SingleAsync()).RevokedAt);
        }
        using var oldAccess = await RawAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, access, false); Assert.Equal(HttpStatusCode.OK, oldAccess.StatusCode);
    }

    [Fact]
    public async Task Logout_after_access_expiry_revokes_current_family_and_logout_all_leaves_other_accounts_active()
    {
        var account = await RegisterAsync(); var otherAccount = await RegisterAsync();
        using var first = await LoginAsync(client, account); var refresh = Cookie(first, IdentityAuthentication.RefreshCookie);
        using var second = factory.CreateHttpsClient(); using (var login = await LoginAsync(second, account)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        clock.Advance(TimeSpan.FromMinutes(5));
        using (var logout = await client.PostBrowserAsync("/api/v1/identity/logout")) Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using (var denied = await RawAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh, true)) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var current = await second.PostBrowserAsync("/api/v1/identity/refresh")) Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        using var other = factory.CreateHttpsClient(); using (var login = await LoginAsync(other, otherAccount)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var all = await second.PostBrowserAsync("/api/v1/identity/logout-all")) Assert.Equal(HttpStatusCode.NoContent, all.StatusCode);
        using (var denied = await second.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var unaffected = await other.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
        using (var again = await client.PostBrowserAsync("/api/v1/identity/logout")) Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        await using var context = database.CreateContext(); Assert.Equal(2, await context.Set<AccountSession>().CountAsync(value => value.UserId == account.Id && value.RevokedAt != null));
        Assert.Null((await context.Set<AccountSession>().SingleAsync(value => value.UserId == otherAccount.Id)).RevokedAt);
    }

    [Fact]
    public async Task Revoke_requires_CSRF_and_ownership_and_can_remove_current_or_another_own_session()
    {
        var account = await RegisterAsync(); var otherAccount = await RegisterAsync(); var adminAccount = await RegisterAsync(AccountType.Admin);
        using (var login = await LoginAsync(client, account)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var current = await client.GetFromJsonAsync<SessionActor>("/api/v1/identity/me");
        using var second = factory.CreateHttpsClient(); using (var login = await LoginAsync(second, account)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var ownOther = await second.GetFromJsonAsync<SessionActor>("/api/v1/identity/me");
        using var other = factory.CreateHttpsClient(); using (var login = await LoginAsync(other, otherAccount)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var foreign = await other.GetFromJsonAsync<SessionActor>("/api/v1/identity/me");
        using (var missing = await client.DeleteAsync("/api/v1/identity/sessions/" + ownOther!.SessionId)) Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var missing = await client.PostAsync("/api/v1/identity/logout-all", null)) Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using (var denied = await client.DeleteBrowserAsync("/api/v1/identity/sessions/" + foreign!.SessionId + "?userId=" + otherAccount.Id)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var admin = factory.CreateHttpsClient(); using (var login = await LoginAsync(admin, adminAccount)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var denied = await admin.DeleteBrowserAsync("/api/v1/identity/sessions/" + foreign.SessionId)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var revoked = await client.DeleteBrowserAsync("/api/v1/identity/sessions/" + ownOther.SessionId)) Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using (var denied = await second.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using (var kept = await client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        using (var unaffected = await other.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode);
        using (var self = await client.DeleteBrowserAsync("/api/v1/identity/sessions/" + current!.SessionId)) Assert.Equal(HttpStatusCode.NoContent, self.StatusCode);
        using var stopped = await client.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, stopped.StatusCode);
    }

    [Fact]
    public async Task Password_reset_intent_reaches_real_Mailpit_once_with_its_own_purpose()
    {
        var account = await RegisterAsync(); await RequestAsync(client, account); var reset = await PayloadAsync(account, OtpPurpose.PasswordReset);
        await PumpAsync(); await PumpAsync(); var mail = await MailpitProbe.MessagesAsync(account.Email);
        Assert.Equal(2, mail.Length);
        Assert.Single(mail, text => text.Contains("Purpose: PasswordReset", StringComparison.Ordinal) && text.Contains(reset.Code, StringComparison.Ordinal));
        await using var context = database.CreateContext(); Assert.Equal(2, await context.Set<OutboxMessage>().CountAsync(value => value.State == OutboxState.Sent && value.ProtectedPayload == ""));
    }

    [Fact]
    public async Task Reset_stays_inside_the_account_type_when_customer_and_merchant_share_identifiers()
    {
        var customer = await RegisterAsync(); var merchantPassword = Password();
        using (var registration = await client.PostBrowserJsonAsync("/api/v1/identity/merchants/register", new { username = customer.Name, email = customer.Email, password = merchantPassword, acceptTerms = true }))
            Assert.Equal(HttpStatusCode.NoContent, registration.StatusCode);
        await RequestAsync(client, customer); var payload = await PayloadAsync(customer, OtpPurpose.PasswordReset);
        using (var wrongType = await client.PostBrowserJsonAsync("/api/v1/identity/password/reset", new { accountType = "Merchant", email = customer.Email, code = payload.Code, newPassword = Password() }))
            Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        using (var wrongOwner = await client.PostBrowserJsonAsync("/api/v1/identity/password/reset", new { accountType = "Customer", email = "other-" + customer.Email, code = payload.Code, newPassword = Password() }))
            Assert.Equal(HttpStatusCode.BadRequest, wrongOwner.StatusCode);
        using (var correct = await ResetAsync(client, customer, payload.Code, Password())) Assert.Equal(HttpStatusCode.NoContent, correct.StatusCode);
        using var merchantLogin = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Merchant", identifier = customer.Email, password = merchantPassword });
        Assert.Equal(HttpStatusCode.NoContent, merchantLogin.StatusCode);
    }
}
