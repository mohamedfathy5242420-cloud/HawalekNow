using System.Net;
using System.Reflection;
using System.Text.Json;
using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Shared.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace HawalekNow.IntegrationTests;

public sealed class ApiFoundationTests
{
    private const string Header = CorrelationIdMiddleware.HeaderName;
    private const string Secret = "internal-password-otp-token-connection-string-marker";

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Real_program_exposes_only_basic_liveness_in_both_environments(string environment)
    {
        await using var factory = new ApiFactory(environment);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-live");
        using var response = await client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("s07-live", Assert.Single(response.Headers.GetValues(Header)));
        var checks = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();
        Assert.Empty(checks.Value.Registrations); // Explicitly basic liveness, never dependency readiness.
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/api/health/live")]
    [InlineData("/api/v2/health/live")]
    [InlineData("/api/v1/health/ready")]
    [InlineData("/api/v1/missing")]
    [InlineData("/openapi/v2.json")]
    [InlineData("/api/v1/_tests/routes")]
    public async Task Unmatched_routes_have_safe_problem_details(string path)
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-missing");
        using var response = await client.GetAsync(path);
        await AssertProblem(response, HttpStatusCode.NotFound, "resource.not_found", "s07-missing");
    }

    [Fact]
    public async Task Unsupported_method_preserves_405_and_allow()
    {
        await using var factory = new ApiFactory("Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-method");
        using var response = await client.PostAsync("/api/v1/health/live", null);
        await AssertProblem(response, HttpStatusCode.MethodNotAllowed, "request.method_not_allowed", "s07-method");
        Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    [InlineData("/scalar/scalar.js")]
    [InlineData("/openapi/v1.json")]
    public async Task Production_does_not_map_documentation_or_assets(string path)
    {
        await using var factory = new ApiFactory("Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-production");
        using var response = await client.GetAsync(path);
        await AssertProblem(response, HttpStatusCode.NotFound, "resource.not_found", "s07-production");
    }

    [Fact]
    public async Task Development_document_contains_only_real_routes_and_exact_media_contracts()
    {
        await using var factory = new ApiFactory("Development", services =>
            services.AddControllers().ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new RouteProbeProvider())));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-document");
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("s07-document", Assert.Single(response.Headers.GetValues(Header)));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.StartsWith("3.1", root.GetProperty("openapi").GetString());
        var paths = root.GetProperty("paths");
        Assert.Equal(new[] { "/api/v1/health/live", "/api/v1/identity/customers/register", "/api/v1/identity/email/request", "/api/v1/identity/email/verify", "/api/v1/identity/merchants/register" },
            paths.EnumerateObject().Select(path => path.Name).Order().ToArray());
        var operation = paths.GetProperty("/api/v1/health/live").GetProperty("get");
        var responses = operation.GetProperty("responses");
        Assert.Equal(new[] { "200", "500", "503" }, responses.EnumerateObject().Select(p => p.Name).Order().ToArray());
        foreach (var status in new[] { "200", "503" })
        {
            var result = responses.GetProperty(status);
            Assert.Equal("text/plain", Assert.Single(result.GetProperty("content").EnumerateObject()).Name);
            Assert.Equal("string", result.GetProperty("content").GetProperty("text/plain").GetProperty("schema").GetProperty("type").GetString());
        }
        var error = responses.GetProperty("500").GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");
        Assert.False(error.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" },
            error.GetProperty("required").EnumerateArray().Select(p => p.GetString()).Order().ToArray());
        Assert.All(responses.EnumerateObject(), item => Assert.True(item.Value.GetProperty("headers").TryGetProperty(Header, out _)));
        var header = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal(Header, header.GetProperty("name").GetString());
        Assert.Equal("header", header.GetProperty("in").GetString());
        Assert.DoesNotContain("_tests", paths.GetRawText());
        // The same host really registered the hidden controller, so absence from the document is meaningful.
        using var probe = await client.GetAsync("/api/v1/_tests/routes");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.Equal("test-only", await probe.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Development_scalar_serves_html_and_local_javascript()
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/scalar");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"url\":\"openapi/v1.json\"", html);
        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        var script = System.Text.RegularExpressions.Regex.Match(html, "src=\"([^\"]+\\.js)\"").Groups[1].Value;
        Assert.NotEmpty(script);
        using var asset = await client.GetAsync(new Uri(response.RequestMessage!.RequestUri!, script));
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("javascript", asset.Content.Headers.ContentType?.MediaType);
        Assert.True((await asset.Content.ReadAsByteArrayAsync()).Length > 1000);
        // Browser rendering/document loading is a separate V3 check; HTML/JS alone does not prove it.
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid value")]
    [InlineData("valid_id-7")]
    public async Task Health_preserves_the_existing_correlation_policy(string? incoming)
    {
        await using var factory = new ApiFactory("Development");
        using var client = factory.CreateClient();
        if (incoming is not null) client.DefaultRequestHeaders.TryAddWithoutValidation(Header, incoming);
        using var response = await client.GetAsync("/api/v1/health/live");
        var actual = Assert.Single(response.Headers.GetValues(Header));
        if (incoming == "valid_id-7") Assert.Equal(incoming, actual);
        else Assert.Matches("^[a-f0-9]{32}$", actual);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unhealthy_liveness_retains_503_plain_body_and_hides_check_details()
    {
        await using var factory = new ApiFactory("Production", services => services.AddHealthChecks()
            .AddCheck("private-" + Secret, () => HealthCheckResult.Unhealthy(Secret, new Exception(Secret),
                new Dictionary<string, object> { ["connectionString"] = Secret }), tags: ["live"]));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-unhealthy");
        using var response = await client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        Assert.Equal("s07-unhealthy", Assert.Single(response.Headers.GetValues(Header)));
        Assert.DoesNotContain(Secret, response.ToString());
    }

    [Fact]
    public async Task Dependency_checks_do_not_turn_liveness_into_readiness()
    {
        await using var factory = new ApiFactory("Production", services => services.AddHealthChecks()
            .AddCheck("dependency", () => throw new InvalidOperationException("Dependency must not be called"), tags: ["ready"]));
        using var client = factory.CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/api/v1/health/live"));
    }

    [Fact]
    public async Task Unexpected_health_service_exception_uses_existing_safe_500_contract()
    {
        await using var factory = new ApiFactory("Production", services =>
            services.Replace(ServiceDescriptor.Singleton<HealthCheckService, ThrowingHealthService>()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(Header, "s07-failure");
        using var response = await client.GetAsync("/api/v1/health/live");
        await AssertProblem(response, HttpStatusCode.InternalServerError, "server.error", "s07-failure");
    }

    [Fact]
    public async Task Real_program_hosts_can_run_and_dispose_independently_without_changing_static_logger()
    {
        var initial = Log.Logger;
        var first = new ApiFactory("Development");
        await using var second = new ApiFactory("Production");
        using var one = first.CreateClient();
        using var two = second.CreateClient();
        Assert.Same(initial, Log.Logger);
        await Task.WhenAll(one.GetStringAsync("/api/v1/health/live"), two.GetStringAsync("/api/v1/health/live"));
        await first.DisposeAsync();
        Assert.Equal("Healthy", await two.GetStringAsync("/api/v1/health/live"));
        Assert.Same(initial, Log.Logger);
    }

    [Theory]
    [InlineData("/escape", false)]
    [InlineData("~/escape", false)]
    [InlineData("/escape", true)]
    [InlineData("~/escape", true)]
    public void Absolute_controller_and_action_routes_cannot_escape_the_prefix(string template, bool onAction)
    {
        var model = new ApplicationModel();
        var controller = new ControllerModel(typeof(RouteProbeController).GetTypeInfo(), []);
        model.Controllers.Add(controller);
        controller.Selectors.Add(new SelectorModel());
        var selector = controller.Selectors[0];
        if (onAction)
        {
            var action = new ActionModel(typeof(RouteProbeController).GetMethod(nameof(RouteProbeController.Get))!, []);
            controller.Actions.Add(action);
            selector = new SelectorModel();
            action.Selectors.Add(selector);
        }
        selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(template));
        Assert.Throws<InvalidOperationException>(() => new ApiV1RouteConvention().Apply(model));
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code, string correlation)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlation, Assert.Single(response.Headers.GetValues(Header)));
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" }, root.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal(correlation, root.GetProperty("correlationId").GetString());
        Assert.Equal("about:blank", root.GetProperty("type").GetString());
        Assert.DoesNotContain(Secret, body);
    }

    private sealed class ApiFactory(string environment, Action<IServiceCollection>? configure = null) : WebApplicationFactory<Program>
    {
        private readonly string identifierKey = TestIdentityConfiguration.NewKey();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(TestIdentityConfiguration.Values(
                "Server=unused.invalid;Database=HawalekNow_HostContract;Integrated Security=True;Encrypt=True", identifierKey)));
            // No copied service/pipeline configuration: all normal registrations and mappings come from Program.
            if (configure is not null) builder.ConfigureTestServices(configure);
        }
    }

    private sealed class RouteProbeProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) => feature.Controllers.Add(typeof(RouteProbeController).GetTypeInfo());
    }

    private sealed class ThrowingHealthService : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Secret);
    }
}

[ApiController]
[Route("_tests/routes")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class RouteProbeController : ControllerBase
{
    [HttpGet]
    public ContentResult Get() => Content("test-only", "text/plain");
}
