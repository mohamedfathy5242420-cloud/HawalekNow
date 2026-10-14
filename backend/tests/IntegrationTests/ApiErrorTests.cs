using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Shared;
using HawalekNow.Application.Shared.Diagnostics;
using HawalekNow.Application.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HawalekNow.IntegrationTests;

public sealed class ApiErrorTests
{
    private static async Task<(WebApplication App, HttpClient Client, PipelineTests.EventSink Sink)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var sink = new PipelineTests.EventSink();
        builder.Services.AddSerilog(configuration => configuration.ConfigureSafePipelineLogging().WriteTo.Sink(sink));
        builder.Services.AddApplication(typeof(ApiErrorTests).Assembly);
        builder.Services.AddScoped<PipelineTests.Probe>();
        builder.Services.AddSingleton<ClientCancellationProbe>();
        builder.Services.AddApiErrorHandling().AddApplicationPart(typeof(ErrorProbeController).Assembly);
        var app = builder.Build();
        app.UseApiErrorHandling();
        app.MapControllers();
        app.MapGet("/client-cancel", async (HttpContext context, ClientCancellationProbe probe) =>
        {
            probe.Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, context.RequestAborted); }
            finally { probe.Ended.TrySetResult(); }
        });
        // Every probe is test-only; production uses the same DI and middleware extensions.
        app.MapGet("/aborted", (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(true);
            throw new OperationCanceledException(context.RequestAborted);
        });
        app.MapGet("/disconnected", (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(true);
            throw new IOException("disconnect-secret");
        });
        app.MapGet("/unrelated-cancellation", () => { throw new OperationCanceledException("exception-secret"); });
        app.MapGet("/started", async (HttpContext context) =>
        {
            await context.Response.WriteAsync("already-started");
            await context.Response.Body.FlushAsync();
            throw new InvalidOperationException("exception-secret");
        });
        await app.StartAsync();
        return (app, app.GetTestClient(), sink);
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, int status, string code,
        string? expectedId = "http-review-123")
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "password-secret", "otp-secret", "token-secret", "exception-secret",
                     "validation-secret", "header-secret", "cookie-secret", "authorization-secret", "query-secret", "disconnect-secret" })
            Assert.DoesNotContain(secret, raw);
        var problem = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal("about:blank", problem.GetProperty("type").GetString());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" },
            problem.EnumerateObject().Select(property => property.Name).Order());
        var id = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.Equal(id, problem.GetProperty("correlationId").GetString());
        if (expectedId is not null) Assert.Equal(expectedId, id);
        else Assert.True(Guid.TryParseExact(id, "N", out _));
        return problem;
    }

    private static void AssertLogsSafe(PipelineTests.EventSink sink)
    {
        Assert.All(sink.Events, entry =>
        {
            foreach (var key in new[] { "RequestId", "RequestPath", "ConnectionId", "ActionId", "ActionName" })
                Assert.False(entry.Properties.ContainsKey(key));
        });
        Assert.All(sink.Events, entry => Assert.Null(entry.Exception));
        var rendered = string.Join("\n", sink.Events.Select(entry => entry.RenderMessage() + string.Join(";", entry.Properties)));
        foreach (var secret in new[] { "password-secret", "otp-secret", "token-secret", "exception-secret",
                     "validation-secret", "header-secret", "cookie-secret", "authorization-secret", "query-secret" })
            Assert.DoesNotContain(secret, rendered);
        Assert.All(sink.Events, entry => Assert.Equal("http-review-123",
            ((Serilog.Events.ScalarValue)entry.Properties["CorrelationId"]).Value));
    }

    private static void Prepare(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "http-review-123");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer authorization-secret");
        client.DefaultRequestHeaders.Add("Cookie", "cookie-secret");
        client.DefaultRequestHeaders.Add("X-Probe", "header-secret");
    }

    [Theory]
    [InlineData(ErrorKind.NotFound, 404, "resource.not_found")]
    [InlineData(ErrorKind.Conflict, 409, "resource.conflict")]
    [InlineData(ErrorKind.Validation, 400, "request.invalid")]
    [InlineData(ErrorKind.Unauthorized, 401, "request.unauthorized")]
    [InlineData(ErrorKind.Forbidden, 403, "request.forbidden")]
    public async Task Expected_result_errors_map_over_http(ErrorKind kind, int status, string code)
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            await AssertProblem(await client.GetAsync($"/probe/result/{kind}?private=query-secret"), status, code);
            await AssertProblem(await client.GetAsync($"/probe/generic-result/{kind}"), status, code);
            AssertLogsSafe(sink);
            // S04 Outcome describes successful execution of Send, including a returned expected failure.
            Assert.Equal(new[] { PipelineLog.Started, PipelineLog.Measured, PipelineLog.Completed },
                sink.Events.Take(3).Select(entry => entry.MessageTemplate.Text));
            Assert.Equal("Succeeded", ((Serilog.Events.ScalarValue)sink.Events[2].Properties["Outcome"]).Value);
        }
    }

    [Fact]
    public async Task Successful_results_preserve_value_or_no_content()
    {
        var (app, client, _) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            var response = await client.GetAsync("/probe/success");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(42, await response.Content.ReadFromJsonAsync<int>());
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("http-review-123", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
            var empty = await client.GetAsync("/probe/empty");
            Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
            Assert.Equal("", await empty.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("validation", 400, "request.invalid")]
    [InlineData("exception", 500, "server.error")]
    public async Task Pipeline_rejection_and_unexpected_exception_are_safe(string mode, int status, string code)
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            await AssertProblem(await client.PostAsJsonAsync($"/probe/send/{mode}?private=query-secret",
                new SecretInput { Password = "password-secret", OTP = "otp-secret", Tokens = "token-secret" }), status, code);
            AssertLogsSafe(sink);
            Assert.Equal(new[] { PipelineLog.Started, PipelineLog.Measured, PipelineLog.Completed },
                sink.Events.Select(entry => entry.MessageTemplate.Text));
            Assert.Equal("Failed", ((Serilog.Events.ScalarValue)sink.Events.Last().Properties["Outcome"]).Value);
        }
    }

    [Theory]
    [InlineData("{bad-password-secret", "application/json", 400, "request.invalid")]
    [InlineData("{\"Number\":\"otp-secret\",\"Password\":\"password-secret\"}", "application/json", 400, "request.invalid")]
    [InlineData("{}", "application/json", 400, "request.invalid")]
    [InlineData("password-secret", "text/plain", 415, "request.unsupported_media_type")]
    public async Task Binding_and_automatic_validation_are_safe(string body, string media, int status, string code)
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            await AssertProblem(await client.PostAsync("/probe/binding", new StringContent(body, Encoding.UTF8, media)), status, code);
            Assert.Empty(sink.Events);
        }
    }

    [Theory]
    [InlineData("/does-not-exist?secret=query-secret", "GET", 404, "resource.not_found")]
    [InlineData("/probe/success", "POST", 405, "request.method_not_allowed")]
    [InlineData("/probe/mvc-not-found", "GET", 404, "resource.not_found")]
    [InlineData("/probe/other-client-error", "GET", 418, "request.failed")]
    public async Task Routing_and_mvc_client_errors_keep_http_semantics(string path, string method, int status, string code)
    {
        var (app, client, _) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            await AssertProblem(response, status, code);
            if (status == 405) Assert.Contains("GET", response.Content.Headers.Allow);
        }
    }

    [Fact]
    public async Task Invalid_correlation_is_replaced_and_problem_json_is_written_for_other_accept_types()
    {
        var (app, client, _) = await StartAsync();
        await using (app)
        using (client)
        {
            client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "bad id");
            client.DefaultRequestHeaders.Add("Accept", "text/html");
            await AssertProblem(await client.GetAsync("/missing"), 404, "resource.not_found", null);
        }
    }

    [Theory]
    [InlineData("/aborted")]
    [InlineData("/disconnected")]
    public async Task Aborted_requests_do_not_become_500_or_write_a_problem(string path)
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(499, (int)response.StatusCode);
            Assert.Equal("", await response.Content.ReadAsStringAsync());
            Assert.Empty(sink.Events);
        }
    }

    [Fact]
    public async Task Cancellation_unrelated_to_aborted_request_is_an_unexpected_failure()
    {
        var (app, client, _) = await StartAsync();
        await using (app)
        using (client)
        {
            Prepare(client);
            await AssertProblem(await client.GetAsync("/unrelated-cancellation"), 500, "server.error");
        }
    }

    [Fact]
    public async Task Client_cancellation_reaches_the_server_without_an_internal_error()
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        using (var cancellation = new CancellationTokenSource())
        {
            var probe = app.Services.GetRequiredService<ClientCancellationProbe>();
            var request = client.GetAsync("/client-cancel", cancellation.Token);
            await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
            await probe.Ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(sink.Events);
        }
    }

    [Fact]
    public async Task A_started_response_is_not_rewritten_and_connection_is_failed()
    {
        var (app, client, sink) = await StartAsync();
        await using (app)
        using (client)
        {
            var response = await client.GetAsync("/started", HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync();
            var bytes = new byte[15];
            await stream.ReadExactlyAsync(bytes);
            Assert.Equal("already-started", Encoding.UTF8.GetString(bytes));
            await Assert.ThrowsAnyAsync<Exception>(async () => await stream.ReadExactlyAsync(new byte[1]));
            Assert.Empty(sink.Events);
        }
    }
}

public sealed class ClientCancellationProbe
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

[ApiController]
[Route("probe")]
public sealed class ErrorProbeController(ISender sender) : ControllerBase
{
    [HttpGet("result/{kind}")]
    public async Task<IActionResult> Expected(ErrorKind kind) =>
        (await sender.Send(new ExpectedCommand(kind), HttpContext.RequestAborted)).ToActionResult(HttpContext);

    [HttpGet("generic-result/{kind}")]
    public IActionResult GenericExpected(ErrorKind kind) => Result<int>.Failure(new Error(kind)).ToActionResult(HttpContext);

    [HttpGet("success")]
    public IActionResult Success() => Result<int>.Success(42).ToActionResult(HttpContext);

    [HttpGet("empty")]
    public IActionResult EmptySuccess() => Result.Success().ToActionResult(HttpContext);

    [HttpGet("mvc-not-found")]
    public IActionResult MvcNotFound() => NotFound();

    [HttpGet("other-client-error")]
    public IActionResult OtherClientError() => StatusCode(418);

    [HttpPost("binding")]
    public IActionResult Binding(BindingInput input) => Ok(input.Number);

    [HttpPost("send/{mode}")]
    public async Task<IActionResult> Send(string mode, SecretInput input) =>
        (await sender.Send(new SecretCommand(mode, input), HttpContext.RequestAborted)).ToActionResult(HttpContext);
}

public sealed class BindingInput
{
    public int Number { get; set; }
    [Required(ErrorMessage = "validation-secret")] public string? Password { get; set; }
}

public sealed class SecretInput
{
    public string? Password { get; set; }
    public string? OTP { get; set; }
    public string? Tokens { get; set; }
}

public sealed record ExpectedCommand(ErrorKind Kind) : IRequest<Result>;
public sealed class ExpectedCommandHandler : IRequestHandler<ExpectedCommand, Result>
{
    public Task<Result> Handle(ExpectedCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure(new Error(request.Kind)));
}

public sealed record SecretCommand(string Mode, SecretInput Input) : IRequest<Result<int>>;
public sealed class SecretCommandValidator : AbstractValidator<SecretCommand>
{
    public SecretCommandValidator()
    {
        RuleFor(command => command).Custom((command, context) =>
        {
            if (command.Mode != "validation") return;
            foreach (var value in new[] { command.Input.Password, command.Input.OTP, command.Input.Tokens })
                context.AddFailure(new ValidationFailure("Password-" + value, "validation-secret " + value, value)
                { ErrorCode = "code-" + value, CustomState = command.Input });
        });
    }
}

public sealed class SecretCommandHandler : IRequestHandler<SecretCommand, Result<int>>
{
    public Task<Result<int>> Handle(SecretCommand request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("exception-secret password-secret otp-secret token-secret");
}
