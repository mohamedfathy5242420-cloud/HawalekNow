using System.Net;
using System.Net.Http.Json;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class BrowserSecurityTests : IAsyncLifetime
{
    private readonly SqlDatabase database = new();
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync() { await database.InitializeAsync(); factory = new(database); client = factory.CreateHttpsClient(); }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await database.DisposeAsync(); }
    private static object Input() => new
    {
        username = "browser" + Guid.NewGuid().ToString("N"),
        email = Guid.NewGuid().ToString("N") + "@example.invalid",
        password = "Test-only-" + Guid.NewGuid().ToString("N"),
        acceptTerms = true,
        accountType = "Customer",
        identifier = "unknown@example.invalid",
        code = "000000"
    };

    [Theory]
    [InlineData("customers/register")]
    [InlineData("merchants/register")]
    [InlineData("login")]
    [InlineData("email/request")]
    [InlineData("email/verify")]
    [InlineData("refresh")]
    [InlineData("password/request")]
    [InlineData("password/reset")]
    [InlineData("logout")]
    public async Task Every_existing_identity_mutation_rejects_missing_or_invalid_CSRF_before_business_work(string route)
    {
        using (var missing = await client.PostAsJsonAsync("/api/v1/identity/" + route, Input()))
        { Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType); }
        using (var token = await client.GetAsync("/api/v1/identity/csrf")) Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/" + route) { Content = JsonContent.Create(Input()) };
        invalid.Headers.Add(BrowserSecurity.CsrfHeader, "invalid-test-token");
        using var response = await client.SendAsync(invalid); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = database.CreateContext(); Assert.Empty(await context.Set<ApplicationUser>().ToArrayAsync());
    }

    [Fact]
    public async Task Token_requires_its_own_cookie_and_HTTPS_and_is_not_an_authentication_token()
    {
        using var token = await client.GetAsync("/api/v1/identity/csrf"); Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.True(token.Headers.CacheControl is { NoCache: true, NoStore: true }, "CSRF response must prohibit caching.");
        var csrf = await token.Content.ReadFromJsonAsync<CsrfTokenResponse>(); Assert.NotNull(csrf); Assert.Equal(BrowserSecurity.CsrfHeader, csrf.HeaderName);
        var header = Assert.Single(token.Headers.GetValues("Set-Cookie"));
        Assert.True(header.StartsWith(BrowserSecurity.CsrfCookie + "=", StringComparison.Ordinal), "Wrong CSRF cookie name.");
        Assert.True(header.Contains("; secure", StringComparison.OrdinalIgnoreCase) && header.Contains("; httponly", StringComparison.OrdinalIgnoreCase) &&
            header.Contains("; samesite=strict", StringComparison.OrdinalIgnoreCase) && header.Contains("; path=/", StringComparison.OrdinalIgnoreCase), "CSRF cookie lacks browser restrictions.");
        Assert.False(header.Contains("domain=", StringComparison.OrdinalIgnoreCase), "CSRF cookie must omit Domain.");
        using var second = factory.CreateHttpsClient();
        using var stolen = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/customers/register") { Content = JsonContent.Create(Input()) };
        stolen.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        using (var response = await second.SendAsync(stolen)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var me = await client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var insecure = factory.CreateClient(new() { BaseAddress = new("http://localhost") });
        insecure.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        using var http = await insecure.GetAsync("/api/v1/identity/csrf"); Assert.Equal(HttpStatusCode.Forbidden, http.StatusCode); Assert.False(http.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Principal_change_requires_a_new_request_token_while_cookie_secret_remains_the_same()
    {
        var name = "csrf" + Guid.NewGuid().ToString("N"); var email = name + "@example.invalid"; var password = "Test-only-" + Guid.NewGuid().ToString("N");
        using (var registration = await client.PostBrowserJsonAsync("/api/v1/identity/customers/register", new { username = name, email, password, acceptTerms = true })) Assert.Equal(HttpStatusCode.NoContent, registration.StatusCode);
        var anonymous = await client.GetFromJsonAsync<CsrfTokenResponse>("/api/v1/identity/csrf");
        using (var login = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Customer", identifier = email, password })) Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/email/request") { Content = JsonContent.Create(new { accountType = "Customer", email }) };
        stale.Headers.Add(anonymous!.HeaderName, anonymous.RequestToken);
        using (var response = await client.SendAsync(stale)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var fresh = await client.PostBrowserJsonAsync("/api/v1/identity/email/request", new { accountType = "Customer", email }); Assert.Equal(HttpStatusCode.NoContent, fresh.StatusCode);
    }

    [Theory]
    [InlineData("customers/register", 5)]
    [InlineData("login", 10)]
    [InlineData("email/request", 5)]
    [InlineData("email/verify", 20)]
    [InlineData("refresh", 30)]
    public async Task Actual_HTTP_quota_rejects_the_next_request_with_safe_429(string route, int permitted)
    {
        // Invalid business input keeps the test focused on HTTP admission, after real CSRF validation.
        for (var count = 0; count < permitted; count++)
        {
            using var response = await client.PostBrowserJsonAsync("/api/v1/identity/" + route, new { invalid = true });
            Assert.True((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests, "Quota rejected a request too early.");
        }
        using var blocked = await client.PostBrowserJsonAsync("/api/v1/identity/" + route, new { invalid = true });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode); Assert.Equal("application/problem+json", blocked.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(blocked.Headers.RetryAfter); Assert.True(blocked.Headers.Contains("X-Correlation-ID"));
        await using var context = database.CreateContext(); Assert.Empty(await context.Set<ApplicationUser>().ToArrayAsync());
    }

    [Fact]
    public async Task Spoofed_forwarding_headers_do_not_change_the_actual_peer_partition()
    {
        using var first = new HttpClient(factory.Server.CreateHandler(context => context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10"))) { BaseAddress = new("https://localhost") };
        using var other = new HttpClient(factory.Server.CreateHandler(context => context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.11"))) { BaseAddress = new("https://localhost") };
        async Task<HttpResponseMessage> SendAsync(HttpClient peer, int index)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/customers/register") { Content = JsonContent.Create(new { invalid = true }) };
            request.Headers.Add("X-Forwarded-For", "198.51.100." + index); request.Headers.Add("X-Real-IP", "192.0.2." + index);
            request.Headers.Add("Forwarded", "for=198.51.100." + index + ";proto=https");
            await BrowserRequests.AddCsrfAsync(peer, request, rawCookie: ""); return await peer.SendAsync(request);
        }
        for (var index = 1; index <= 5; index++) { using var response = await SendAsync(first, index); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using (var blocked = await SendAsync(first, 6)) Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var admitted = await SendAsync(other, 6); Assert.Equal(HttpStatusCode.BadRequest, admitted.StatusCode);
    }

    [Fact]
    public async Task Explicit_owned_key_ring_preserves_CSRF_across_host_restart_without_machine_trust_changes()
    {
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var ring = Path.GetFullPath(Path.Combine(parent, "HN_P02_Keys_" + Guid.NewGuid().ToString("N")));
        if (Path.GetDirectoryName(ring) != parent || Directory.Exists(ring)) throw new InvalidOperationException("Test key directory is not new and owned.");
        Directory.CreateDirectory(ring);
        try
        {
            var settings = new Dictionary<string, string?> { ["Security:KeyRingPath"] = ring };
            CsrfTokenResponse csrf; string cookie;
            await using (var first = new IdentityApiFactory(database, settings: settings, ephemeralProtection: false))
            {
                using var browser = first.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
                using var token = await browser.GetAsync("/api/v1/identity/csrf"); Assert.Equal(HttpStatusCode.OK, token.StatusCode);
                csrf = (await token.Content.ReadFromJsonAsync<CsrfTokenResponse>())!;
                cookie = token.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(BrowserSecurity.CsrfCookie + "=", StringComparison.Ordinal)).Split(';')[0];
            }
            Assert.NotEmpty(Directory.GetFiles(ring, "key-*.xml"));
            await using var restarted = new IdentityApiFactory(database, settings: settings, ephemeralProtection: false);
            using var next = restarted.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
            using var mutation = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/customers/register") { Content = JsonContent.Create(Input()) };
            mutation.Headers.Add("Cookie", cookie); mutation.Headers.Add(csrf.HeaderName, csrf.RequestToken);
            using var response = await next.SendAsync(mutation); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            // Only the exact new random directory created above, directly inside the verified temp root.
            if (Path.GetDirectoryName(Path.GetFullPath(ring)) != parent || !Path.GetFileName(ring).StartsWith("HN_P02_Keys_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the owned test key directory.");
            Directory.Delete(ring, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative-test-keyring")]
    public async Task Production_rejects_missing_or_relative_persistent_key_ring_configuration(string path)
    {
        await using var invalid = new IdentityApiFactory(database, settings: new Dictionary<string, string?> { ["Security:KeyRingPath"] = path }, environment: "Production");
        var error = Assert.Throws<InvalidOperationException>(() => invalid.CreateHttpsClient());
        Assert.Contains("KeyRingPath", error.Message);
    }
}
