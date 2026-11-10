using System.Net.Http.Json;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Api.Shared.Security;

namespace HawalekNow.IntegrationTests;

public static class BrowserRequests
{
    // Real antiforgery exchange, refreshed for the current principal before every mutation.
    public static async Task<HttpResponseMessage> PostBrowserJsonAsync<T>(this HttpClient client, string route, T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        await AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    public static async Task<HttpResponseMessage> PostBrowserAsync(this HttpClient client, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        await AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    public static async Task AddCsrfAsync(HttpClient client, HttpRequestMessage request, string? rawCookie = null)
    {
        using var query = new HttpRequestMessage(HttpMethod.Get, "/api/v1/identity/csrf");
        if (!string.IsNullOrEmpty(rawCookie)) query.Headers.Add("Cookie", rawCookie);
        using var response = await client.SendAsync(query);
        Assert.True(response.IsSuccessStatusCode, "CSRF bootstrap failed.");
        var csrf = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        Assert.NotNull(csrf); request.Headers.Add(csrf.HeaderName, csrf.RequestToken);
        if (rawCookie is not null)
        {
            var csrfCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? cookies.SingleOrDefault(value => value.StartsWith(BrowserSecurity.CsrfCookie + "=", StringComparison.Ordinal))?.Split(';')[0] : null;
            var combined = string.IsNullOrEmpty(rawCookie) ? csrfCookie : rawCookie + (csrfCookie is null ? "" : "; " + csrfCookie);
            if (!string.IsNullOrEmpty(combined)) request.Headers.Add("Cookie", combined);
        }
    }
}
