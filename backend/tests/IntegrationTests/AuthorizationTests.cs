using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HawalekNow.IntegrationTests;

public sealed class AuthorizationTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private sealed record TestAccount(Guid Id, string Name, string Email, string Password, AccountType Type);
    private async Task<TestAccount> CreateAsync(AccountType type, bool confirmed, MerchantApprovalStatus status = MerchantApprovalStatus.Pending)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HawalekDbContext>();
        await using var transaction = await context.BeginAsync();
        var name = "policy" + Guid.NewGuid().ToString("N"); var email = name + "@example.invalid";
        var password = "Test-only-" + Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(type, name, email); user.EmailConfirmed = confirmed;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded, "Fixture account creation failed.");
        if (type == AccountType.Merchant)
        {
            await scope.ServiceProvider.GetRequiredService<IMerchantAccounts>().CreatePendingAsync(user.Id);
            await context.SaveChangesAsync();
            var merchant = await context.Set<MerchantAccount>().SingleAsync(value => value.UserId == user.Id);
            context.Entry(merchant).Property(value => value.Status).CurrentValue = status; // approved/suspended fixtures only; no P04 endpoint
        }
        await context.SaveChangesAsync(); await transaction.CommitAsync(); return new(user.Id, name, email, password, type);
    }
    private static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, TestAccount account) =>
        await browser.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = account.Type.ToString(), identifier = account.Email, password = account.Password });
    private async Task UserStatusAsync(Guid id, AccountAccessStatus status)
    {
        await using var context = database.CreateContext(); var user = await context.Set<ApplicationUser>().SingleAsync(value => value.Id == id);
        context.Entry(user).Property(value => value.AccessStatus).CurrentValue = status; await context.SaveChangesAsync();
    }
    private async Task MerchantStatusAsync(Guid id, MerchantApprovalStatus status)
    {
        await using var context = database.CreateContext(); var merchant = await context.Set<MerchantAccount>().SingleAsync(value => value.UserId == id);
        context.Entry(merchant).Property(value => value.Status).CurrentValue = status; await context.SaveChangesAsync();
    }

    public static TheoryData<string, int, int, int, int, int> Matrix => new()
    {
        { "guest", 401, 401, 401, 401, 401 },
        { "unconfirmed-customer", 200, 403, 403, 403, 403 },
        { "unconfirmed-merchant", 200, 403, 403, 403, 403 },
        { "customer", 200, 200, 403, 403, 403 },
        { "pending", 200, 403, 200, 403, 403 },
        { "changes-requested", 200, 403, 200, 403, 403 },
        { "approved", 200, 403, 200, 200, 403 },
        { "rejected", 200, 403, 403, 403, 403 },
        { "merchant-suspended", 401, 401, 401, 401, 401 },
        { "account-suspended", 401, 401, 401, 401, 401 },
        { "deleted", 401, 401, 401, 401, 401 },
        { "revoked", 401, 401, 401, 401, 401 },
        { "admin", 200, 403, 403, 403, 200 },
        { "unconfirmed-admin", 200, 403, 403, 403, 403 }
    };

    [Theory, MemberData(nameof(Matrix))]
    public async Task Actual_HTTP_matrix_uses_SQL_state_including_old_cookies(string scenario, int me, int customer, int setup, int publishing, int admin)
    {
        TestAccount? account = null;
        if (scenario != "guest")
        {
            var type = scenario.Contains("admin", StringComparison.Ordinal) ? AccountType.Admin :
                scenario is "pending" or "changes-requested" or "approved" or "rejected" or "merchant-suspended" or "unconfirmed-merchant" ? AccountType.Merchant : AccountType.Customer;
            var status = scenario switch
            {
                "approved" => MerchantApprovalStatus.Approved,
                "changes-requested" => MerchantApprovalStatus.ChangesRequested,
                "rejected" => MerchantApprovalStatus.Rejected,
                _ => MerchantApprovalStatus.Pending
            };
            account = await CreateAsync(type, !scenario.StartsWith("unconfirmed", StringComparison.Ordinal), status);
            using var login = await LoginAsync(client, account); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            if (scenario == "merchant-suspended") await MerchantStatusAsync(account.Id, MerchantApprovalStatus.Suspended);
            if (scenario == "account-suspended") await UserStatusAsync(account.Id, AccountAccessStatus.Suspended);
            if (scenario == "deleted") await UserStatusAsync(account.Id, AccountAccessStatus.Deleted);
            if (scenario == "revoked")
            {
                await using var context = database.CreateContext(); var session = await context.Set<AccountSession>().SingleAsync(); session.Revoke(clock.GetUtcNow()); await context.SaveChangesAsync();
            }
        }
        foreach (var (route, status) in new[] { ("identity/me", me), ("identity/sessions", me), ("identity/customer-profile", customer),
            ("merchants/account", setup), ("merchants/account/publishing-access", publishing), ("identity/admin/session", admin) })
        {
            using var response = await client.GetAsync("/api/v1/" + route); Assert.Equal((HttpStatusCode)status, response.StatusCode);
            if (status != 200) Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            Assert.False(body.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase) || body.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase), "Sensitive account state was exposed.");
        }
        if (account is not null && me == 401)
        {
            using var refresh = await client.PostBrowserAsync("/api/v1/identity/refresh"); Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
            if (scenario != "revoked") { using var login = await LoginAsync(client, account); Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode); }
        }
    }

    [Fact]
    public async Task Session_queries_use_the_authenticated_owner_and_Admin_has_no_cross_owner_override()
    {
        var own = await CreateAsync(AccountType.Customer, true); var other = await CreateAsync(AccountType.Customer, true); var admin = await CreateAsync(AccountType.Admin, true);
        using (var first = await LoginAsync(client, own)) Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        using (var second = await LoginAsync(client, own)) Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        using var otherClient = factory.CreateHttpsClient(); using (var login = await LoginAsync(otherClient, other)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var otherActor = await otherClient.GetFromJsonAsync<SessionActor>("/api/v1/identity/me");
        var sessions = await client.GetFromJsonAsync<AccountSessionInfo[]>("/api/v1/identity/sessions?userId=" + other.Id);
        Assert.NotNull(sessions); Assert.Equal(2, sessions.Length); Assert.Single(sessions, value => value.IsCurrent);
        foreach (var session in sessions)
        { using var response = await client.GetAsync("/api/v1/identity/sessions/" + session.Id); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using (var foreign = await client.GetAsync("/api/v1/identity/sessions/" + otherActor!.SessionId)) Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var profile = await client.GetFromJsonAsync<CustomerIdentityInfo>("/api/v1/identity/customer-profile?userId=" + other.Id); Assert.Equal(own.Id, profile!.UserId);
        using var adminClient = factory.CreateHttpsClient(); using (var login = await LoginAsync(adminClient, admin)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var adminForeign = await adminClient.GetAsync("/api/v1/identity/sessions/" + sessions[0].Id); Assert.Equal(HttpStatusCode.NotFound, adminForeign.StatusCode);
    }

    [Fact]
    public async Task Email_confirmation_and_merchant_approval_changes_affect_existing_cookies_without_reissue()
    {
        var customer = await CreateAsync(AccountType.Customer, false);
        using (var login = await LoginAsync(client, customer)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var denied = await client.GetAsync("/api/v1/identity/customer-profile")) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using (var context = database.CreateContext()) { var user = await context.Set<ApplicationUser>().SingleAsync(); user.EmailConfirmed = true; await context.SaveChangesAsync(); }
        using (var allowed = await client.GetAsync("/api/v1/identity/customer-profile")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var merchant = await CreateAsync(AccountType.Merchant, true); using (var login = await LoginAsync(client, merchant)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var pending = await client.GetAsync("/api/v1/merchants/account")) Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using (var denied = await client.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await MerchantStatusAsync(merchant.Id, MerchantApprovalStatus.Approved);
        using (var allowed = await client.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await MerchantStatusAsync(merchant.Id, MerchantApprovalStatus.ChangesRequested);
        using (var denied = await client.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var allowed = await client.GetAsync("/api/v1/merchants/account")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await MerchantStatusAsync(merchant.Id, MerchantApprovalStatus.Suspended);
        using var stopped = await client.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, stopped.StatusCode);
    }

    [Fact]
    public async Task Client_roles_user_ids_and_even_signed_extra_claims_cannot_replace_SQL_actor_or_session_ownership()
    {
        var account = await CreateAsync(AccountType.Customer, false); var other = await CreateAsync(AccountType.Customer, true);
        using var login = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Customer", identifier = account.Email, password = account.Password, roles = new[] { "Admin" }, userId = other.Id });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var actor = await client.GetFromJsonAsync<SessionActor>("/api/v1/identity/me"); Assert.Equal(account.Id, actor!.UserId); Assert.Equal("Customer", actor.AccountType);
        var configuration = TestIdentityConfiguration.Create(database.ConnectionString, database.IdentifierKey);
        string Token(Guid subject) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new("sub", subject.ToString("N")), new("sid", actor.SessionId.ToString("N")), new("account_type", "Admin"), new("email_confirmed", "true"), new("role", "Admin")],
            clock.GetUtcNow().UtcDateTime, clock.GetUtcNow().AddMinutes(5).UtcDateTime,
            new(new SymmetricSecurityKey(Convert.FromBase64String(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256)));
        using var raw = factory.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        raw.DefaultRequestHeaders.Add("Cookie", IdentityAuthentication.AccessCookie + "=" + Token(account.Id));
        using (var admin = await raw.GetAsync("/api/v1/identity/admin/session")) Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        using (var confirmed = await raw.GetAsync("/api/v1/identity/customer-profile")) Assert.Equal(HttpStatusCode.Forbidden, confirmed.StatusCode);
        raw.DefaultRequestHeaders.Remove("Cookie"); raw.DefaultRequestHeaders.Add("Cookie", IdentityAuthentication.AccessCookie + "=" + Token(other.Id));
        using (var wrongOwner = await raw.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, wrongOwner.StatusCode);
        using var elevation = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Admin", identifier = account.Email, password = account.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, elevation.StatusCode);
    }

    [Fact]
    public async Task Merchant_account_and_publishing_policy_ignore_another_owners_query_identifier()
    {
        var pending = await CreateAsync(AccountType.Merchant, true); var approved = await CreateAsync(AccountType.Merchant, true, MerchantApprovalStatus.Approved);
        Guid pendingProfile; Guid approvedProfile;
        await using (var context = database.CreateContext())
        {
            pendingProfile = (await context.Set<MerchantAccount>().SingleAsync(value => value.UserId == pending.Id)).Id;
            approvedProfile = (await context.Set<MerchantAccount>().SingleAsync(value => value.UserId == approved.Id)).Id;
        }
        using (var login = await LoginAsync(client, pending)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var own = await client.GetFromJsonAsync<MerchantAccountInfo>("/api/v1/merchants/account?userId=" + approved.Id + "&merchantAccountId=" + approvedProfile);
        Assert.Equal(pendingProfile, own!.Id); Assert.False(own.IsApproved);
        using var denied = await client.GetAsync("/api/v1/merchants/account/publishing-access?userId=" + approved.Id); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var otherClient = factory.CreateHttpsClient(); using (var login = await LoginAsync(otherClient, approved)) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var publishing = await otherClient.GetFromJsonAsync<HawalekNow.Api.Modules.Merchants.PublishingAccess>("/api/v1/merchants/account/publishing-access");
        Assert.Equal(approvedProfile, publishing!.MerchantAccountId); Assert.True(publishing.CanPublish);
    }

    [Fact]
    public async Task SQL_check_rejects_undefined_access_status_without_changing_the_active_account()
    {
        var account = await CreateAsync(AccountType.Customer, true);
        await using (var context = database.CreateContext())
        {
            var user = await context.Set<ApplicationUser>().SingleAsync(); Assert.Equal(AccountAccessStatus.Active, user.AccessStatus);
            context.Entry(user).Property(value => value.AccessStatus).CurrentValue = (AccountAccessStatus)99;
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        await using var verify = database.CreateContext(); Assert.Equal(AccountAccessStatus.Active, (await verify.Set<ApplicationUser>().SingleAsync(value => value.Id == account.Id)).AccessStatus);
    }

    [Theory]
    [InlineData(AccountAccessStatus.Suspended)]
    [InlineData(AccountAccessStatus.Deleted)]
    public async Task Inactive_accounts_cannot_confirm_with_a_preexisting_code_or_emit_its_pending_message(AccountAccessStatus status)
    {
        var account = await CreateAsync(AccountType.Customer, false);
        using (var requested = await client.PostBrowserJsonAsync("/api/v1/identity/email/request", new { accountType = "Customer", email = account.Email })) Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        EmailOtpPayload payload;
        await using (var context = database.CreateContext())
        {
            var message = await context.Set<OutboxMessage>().SingleAsync();
            payload = factory.Services.GetRequiredService<PayloadProtection>().Unprotect<EmailOtpPayload>(message.Kind, message.ProtectedPayload);
        }
        await UserStatusAsync(account.Id, status);
        using (var again = await client.PostBrowserJsonAsync("/api/v1/identity/email/request", new { accountType = "Customer", email = account.Email })) Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        using (var confirmation = await client.PostBrowserJsonAsync("/api/v1/identity/email/verify", new { accountType = "Customer", email = account.Email, code = payload.Code, accessStatus = "Active", userId = account.Id }))
            Assert.Equal(HttpStatusCode.BadRequest, confirmation.StatusCode);
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(CancellationToken.None);
        await using var verify = database.CreateContext();
        Assert.False((await verify.Set<ApplicationUser>().SingleAsync()).EmailConfirmed);
        Assert.Equal(OutboxState.Suppressed, (await verify.Set<OutboxMessage>().SingleAsync()).State);
        Assert.Empty(await MailpitProbe.MessagesAsync(account.Email));
    }
}
