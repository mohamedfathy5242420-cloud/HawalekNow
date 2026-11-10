using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Application.Modules.Identity;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HawalekNow.IntegrationTests;

public sealed class SessionTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private readonly ManualTimeProvider clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    private readonly string name = "session" + Guid.NewGuid().ToString("N");
    private readonly string password = "Test-only-" + Guid.NewGuid().ToString("N");
    private string Email => name + "@example.invalid";
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database, clock); client = factory.CreateHttpsClient(); }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private object Login(string type = "Customer", string? secret = null) => new { accountType = type, identifier = Email, password = secret ?? password };
    private async Task RegisterAsync(string type = "customers")
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/identity/{type}/register", new { username = name, email = Email, password, acceptTerms = true });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private static string Cookie(HttpResponseMessage response, string name) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(name + "=", StringComparison.Ordinal)).Split(';')[0][(name.Length + 1)..];
    private async Task<HttpResponseMessage> WithCookieAsync(string route, string name, string value, bool post = true)
    {
        using var raw = factory.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, route);
        request.Headers.Add("Cookie", name + "=" + value);
        return await raw.SendAsync(request);
    }

    [Fact]
    public async Task Type_scoped_login_uses_cookie_only_and_returns_no_credentials_in_body()
    {
        await RegisterAsync(); await RegisterAsync("merchants");
        using var customer = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        Assert.Equal(HttpStatusCode.NoContent, customer.StatusCode); Assert.True((await customer.Content.ReadAsStringAsync()).Length == 0, "Login body must be empty.");
        Assert.Equal("no-store", customer.Headers.CacheControl?.ToString());
        var first = Cookie(customer, IdentityAuthentication.AccessCookie);
        using (var me = await client.GetAsync("/api/v1/identity/me"))
        { Assert.Equal(HttpStatusCode.OK, me.StatusCode); var actor = await me.Content.ReadFromJsonAsync<SessionActor>(); Assert.Equal("Customer", actor!.AccountType); Assert.False(actor.EmailConfirmed); }
        using var merchant = await client.PostAsJsonAsync("/api/v1/identity/login", Login("Merchant"));
        Assert.Equal(HttpStatusCode.NoContent, merchant.StatusCode);
        using (var me = await client.GetAsync("/api/v1/identity/me")) Assert.Equal("Merchant", (await me.Content.ReadFromJsonAsync<SessionActor>())!.AccountType);
        using var bearerClient = factory.CreateHttpsClient(); bearerClient.DefaultRequestHeaders.Authorization = new("Bearer", first);
        using var bearer = await bearerClient.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, bearer.StatusCode);
        await using var context = database.CreateContext();
        Assert.Equal(2, await context.Set<AccountSession>().CountAsync());
        foreach (var row in await context.Set<RefreshCredential>().ToArrayAsync()) { Assert.Equal(64, row.Digest.Length); Assert.Null(row.ConsumedAt); }
        var redacted = new SessionCredentials("access-secret", "refresh-secret", clock.GetUtcNow(), clock.GetUtcNow());
        Assert.Equal("{}", JsonSerializer.Serialize(redacted)); Assert.DoesNotContain("secret", redacted.ToString());
    }

    [Fact]
    public async Task Real_HTTPS_Kestrel_cookie_container_keeps_Secure_HttpOnly_prefix_and_rotates()
    {
        await RegisterAsync();
        await using var https = new TestHttpsClient(database, clock);
        using var login = await https.Client.PostAsJsonAsync("/api/v1/identity/login", Login());
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal("https", https.Address.Scheme);
        foreach (var header in login.Headers.GetValues("Set-Cookie"))
        {
            Assert.True(header.StartsWith("__Host-", StringComparison.Ordinal), "Host-prefixed cookie required.");
            Assert.True(header.Contains("; path=/", StringComparison.OrdinalIgnoreCase), "Root cookie path required.");
            Assert.True(header.Contains("; secure", StringComparison.OrdinalIgnoreCase), "Secure cookie required.");
            Assert.True(header.Contains("; httponly", StringComparison.OrdinalIgnoreCase), "HttpOnly cookie required.");
            Assert.True(header.Contains("; samesite=strict", StringComparison.OrdinalIgnoreCase), "Strict same-site cookie required.");
            Assert.False(header.Contains("domain=", StringComparison.OrdinalIgnoreCase), "Host cookie must omit Domain.");
        }
        var before = https.Cookies.GetCookies(https.Address)[IdentityAuthentication.RefreshCookie]!.Value;
        Assert.True(https.Cookies.GetCookieHeader(new UriBuilder(https.Address) { Scheme = "http" }.Uri).Length == 0, "Secure credentials were exposed to HTTP.");
        using (var me = await https.Client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using (var refresh = await https.Client.PostAsync("/api/v1/identity/refresh", null))
        { Assert.Equal(HttpStatusCode.NoContent, refresh.StatusCode); Assert.True((await refresh.Content.ReadAsStringAsync()).Length == 0, "Refresh body must be empty."); }
        Assert.False(before == https.Cookies.GetCookies(https.Address)[IdentityAuthentication.RefreshCookie]!.Value, "Refresh credential did not rotate.");
        using (var me = await https.Client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Concurrent_rotation_issues_one_success_then_replay_revokes_that_entire_family()
    {
        await RegisterAsync(); using var login = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        var refresh = Cookie(login, IdentityAuthentication.RefreshCookie); var access = Cookie(login, IdentityAuthentication.AccessCookie);
        var results = await Task.WhenAll(WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh),
            WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh));
        Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.NoContent)); Assert.Equal(1, results.Count(value => value.StatusCode == HttpStatusCode.Unauthorized));
        var next = Cookie(results.Single(value => value.StatusCode == HttpStatusCode.NoContent), IdentityAuthentication.RefreshCookie);
        foreach (var result in results) result.Dispose();
        using (var oldAccess = await WithCookieAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, access, false)) Assert.Equal(HttpStatusCode.Unauthorized, oldAccess.StatusCode);
        using (var nextRefresh = await WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, next)) Assert.Equal(HttpStatusCode.Unauthorized, nextRefresh.StatusCode);
        await using var context = database.CreateContext();
        Assert.NotNull((await context.Set<AccountSession>().SingleAsync()).RevokedAt);
        var rows = await context.Set<RefreshCredential>().ToArrayAsync(); Assert.Equal(2, rows.Length); Assert.Single(rows, value => value.ConsumedAt is not null);
        Assert.All(rows, row => Assert.False(row.Digest == refresh || row.Digest == next, "Raw refresh credential persisted."));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("algorithm")]
    [InlineData("expired")]
    [InlineData("future")]
    public async Task Actual_authentication_rejects_invalid_signature_issuer_audience_algorithm_or_time(string invalid)
    {
        await RegisterAsync(); using var login = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        var original = new JwtSecurityTokenHandler().ReadJwtToken(Cookie(login, IdentityAuthentication.AccessCookie));
        var config = TestIdentityConfiguration.Create(database.ConnectionString, database.IdentifierKey);
        var now = clock.GetUtcNow();
        var key = invalid is "signature" or "algorithm" ? RandomNumberGenerator.GetBytes(64) : Convert.FromBase64String(config["Jwt:SigningKey"]!);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(invalid == "issuer" ? "wrong" : config["Jwt:Issuer"],
            invalid == "audience" ? "wrong" : config["Jwt:Audience"], original.Claims.Where(value => value.Type is "sub" or "sid"),
            (invalid == "future" ? now.AddMinutes(1) : now.AddMinutes(-10)).UtcDateTime,
            (invalid == "expired" ? now.AddSeconds(-1) : now.AddMinutes(5)).UtcDateTime,
            new(new SymmetricSecurityKey(key), invalid == "algorithm" ? SecurityAlgorithms.HmacSha512 : SecurityAlgorithms.HmacSha256)));
        using var response = await WithCookieAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, token, false);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False((await response.Content.ReadAsStringAsync()).Contains(token, StringComparison.Ordinal), "JWT leaked to error response.");
    }

    [Fact]
    public async Task Access_expiry_refresh_idle_expiry_and_database_revocation_apply_on_the_next_request()
    {
        await RegisterAsync(); using var login = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        var access = Cookie(login, IdentityAuthentication.AccessCookie); var refresh = Cookie(login, IdentityAuthentication.RefreshCookie);
        clock.Advance(TimeSpan.FromMinutes(5));
        using (var expired = await WithCookieAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, access, false)) Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var rotated = await WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh);
        Assert.Equal(HttpStatusCode.NoContent, rotated.StatusCode);
        var nextAccess = Cookie(rotated, IdentityAuthentication.AccessCookie); var nextRefresh = Cookie(rotated, IdentityAuthentication.RefreshCookie);
        await using (var context = database.CreateContext()) { var session = await context.Set<AccountSession>().SingleAsync(); session.Revoke(clock.GetUtcNow()); await context.SaveChangesAsync(); }
        using (var revoked = await WithCookieAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, nextAccess, false)) Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using (var revokedRefresh = await WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, nextRefresh)) Assert.Equal(HttpStatusCode.Unauthorized, revokedRefresh.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        var idle = Cookie(newLogin, IdentityAuthentication.RefreshCookie); clock.Advance(TimeSpan.FromDays(7));
        using var idleResponse = await WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, idle); Assert.Equal(HttpStatusCode.Unauthorized, idleResponse.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_persist_lockout_and_unknown_credentials_have_the_same_safe_response()
    {
        await RegisterAsync();
        for (var count = 0; count < 5; count++) { using var wrong = await client.PostAsJsonAsync("/api/v1/identity/login", Login(secret: "wrong-test-password")); Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode); }
        using (var locked = await client.PostAsJsonAsync("/api/v1/identity/login", Login())) Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using (var unknown = await client.PostAsJsonAsync("/api/v1/identity/login", new { accountType = "Customer", identifier = "unknown-" + Email, password }))
        { Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode); Assert.False((await unknown.Content.ReadAsStringAsync()).Contains(password, StringComparison.Ordinal)); }
        await using (var context = database.CreateContext()) { Assert.Empty(await context.Set<AccountSession>().ToArrayAsync()); Assert.NotNull((await context.Set<ApplicationUser>().SingleAsync()).LockoutEnd); }
        clock.Advance(TimeSpan.FromMinutes(15));
        using var success = await client.PostAsJsonAsync("/api/v1/identity/login", Login()); Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
        using var http = factory.CreateClient(new() { BaseAddress = new("http://localhost"), HandleCookies = false });
        using var insecure = await http.PostAsJsonAsync("/api/v1/identity/login", Login()); Assert.Equal(HttpStatusCode.Forbidden, insecure.StatusCode); Assert.False(insecure.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("Jwt:SigningKey", "missing")]
    [InlineData("Jwt:SigningKey", "short")]
    [InlineData("Jwt:Issuer", "missing")]
    [InlineData("Jwt:Audience", "missing")]
    public async Task Actual_Program_rejects_missing_or_weak_JWT_configuration_without_echoing_values(string setting, string invalid)
    {
        var value = invalid == "short" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(8)) : "";
        await using var broken = new IdentityApiFactory(database, settings: new Dictionary<string, string?> { [setting] = value });
        var exception = Assert.Throws<InvalidOperationException>(() => broken.CreateHttpsClient());
        Assert.StartsWith("Configure ", exception.Message);
        Assert.False(value.Length > 0 && exception.Message.Contains(value, StringComparison.Ordinal), "Invalid key appeared in startup failure.");
    }

    [Fact]
    public async Task SQL_session_and_refresh_survive_host_restart_with_stable_keys()
    {
        await RegisterAsync(); using var login = await client.PostAsJsonAsync("/api/v1/identity/login", Login());
        var access = Cookie(login, IdentityAuthentication.AccessCookie); var refresh = Cookie(login, IdentityAuthentication.RefreshCookie);
        client.Dispose(); await factory.DisposeAsync();
        factory = new(database, clock); client = factory.CreateHttpsClient();
        using (var me = await WithCookieAsync("/api/v1/identity/me", IdentityAuthentication.AccessCookie, access, false)) Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var rotated = await WithCookieAsync("/api/v1/identity/refresh", IdentityAuthentication.RefreshCookie, refresh);
        Assert.Equal(HttpStatusCode.NoContent, rotated.StatusCode);
        using var changed = new IdentityApiFactory(database, clock, settings: new Dictionary<string, string?> { ["Jwt:SigningKey"] = TestIdentityConfiguration.NewKey() });
        using var changedClient = changed.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        changedClient.DefaultRequestHeaders.Add("Cookie", IdentityAuthentication.AccessCookie + "=" + access);
        using var rejected = await changedClient.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task Merchant_can_use_normalized_username_or_email_while_customer_uses_email()
    {
        await RegisterAsync(); await RegisterAsync("merchants");
        using (var byName = await client.PostAsJsonAsync("/api/v1/identity/login", new { accountType = "Merchant", identifier = name.ToUpperInvariant(), password }))
            Assert.Equal(HttpStatusCode.NoContent, byName.StatusCode);
        using (var me = await client.GetAsync("/api/v1/identity/me")) Assert.Equal("Merchant", (await me.Content.ReadFromJsonAsync<SessionActor>())!.AccountType);
        using (var byEmail = await client.PostAsJsonAsync("/api/v1/identity/login", Login("Merchant"))) Assert.Equal(HttpStatusCode.NoContent, byEmail.StatusCode);
        using var customerName = await client.PostAsJsonAsync("/api/v1/identity/login", new { accountType = "Customer", identifier = name, password });
        Assert.Equal(HttpStatusCode.BadRequest, customerName.StatusCode);
    }
}
