using FluentValidation;
using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Application.Shared;
using HawalekNow.Application.Shared.Behaviors;
using HawalekNow.Application.Shared.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace HawalekNow.IntegrationTests;

public sealed class PipelineTests
{
    internal static (ServiceProvider Provider, EventSink Sink) CreateProvider()
    {
        var sink = new EventSink();
        var services = new ServiceCollection();
        services.AddLogging();
        // Each provider owns its logger; never replace or close another test's static logger.
        services.AddSerilog(configuration => configuration.ConfigureSafePipelineLogging().WriteTo.Sink(sink),
            preserveStaticLogger: true);
        services.AddApplication(typeof(PipelineTests).Assembly);
        services.AddScoped<Probe>();
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), sink);
    }

    [Fact]
    public async Task Valid_request_returns_result_and_executes_registered_pipeline_in_order()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            var probe = services.GetRequiredService<Probe>();
            probe.Sink = sink;
            services.GetRequiredService<CorrelationContext>().Initialize("review-123");
            var behaviors = services.GetServices<IPipelineBehavior<SampleCommand, string>>().Select(value => value.GetType().GetGenericTypeDefinition());
            Assert.Equal([typeof(LoggingBehavior<,>), typeof(PerformanceBehavior<,>), typeof(ValidationBehavior<,>)], behaviors);
            Assert.Equal(2, services.GetServices<IValidator<SampleCommand>>().Count());
            Assert.Equal("result-secret", await services.GetRequiredService<ISender>().Send(new SampleCommand(1)));
            Assert.Equal(1, probe.HandlerCalls);
            Assert.Equal(["first-validator", "second-validator", "handler"], probe.Trace);
            Assert.Equal([PipelineLog.Started, PipelineLog.Measured, PipelineLog.Completed], sink.Events.Select(value => value.MessageTemplate.Text));
            Assert.All(sink.Events, value => Assert.Equal("review-123", Scalar(value, "CorrelationId")));
            Assert.Equal("Succeeded", Scalar(sink.Events.Last(), "Outcome"));
            Assert.IsType<double>(Scalar(sink.Events[1], "ElapsedMilliseconds"));
            Assert.True((double)Scalar(sink.Events[1], "ElapsedMilliseconds")! >= 0);
            AssertSafe(sink);
        }
    }

    [Fact]
    public async Task Validator_exception_is_propagated_unchanged_and_handler_does_not_run()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            var probe = services.GetRequiredService<Probe>();
            probe.Error = new InvalidOperationException("validation-secret");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ISender>().Send(new SampleCommand(1)));
            Assert.Same(probe.Error, error);
            Assert.Equal(0, probe.HandlerCalls);
            Assert.Equal("Failed", Scalar(sink.Events.Last(), "Outcome"));
            AssertSafe(sink);
        }
    }

    [Fact]
    public async Task Invalid_request_collects_all_failures_without_executing_handler_and_is_measured()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            services.GetRequiredService<Probe>().Sink = sink;
            var error = await Assert.ThrowsAsync<ValidationException>(() => services.GetRequiredService<ISender>().Send(new SampleCommand(-1)));
            Assert.Equal(2, error.Errors.Count());
            Assert.Equal(0, services.GetRequiredService<Probe>().HandlerCalls);
            Assert.Equal([PipelineLog.Started, PipelineLog.Measured, PipelineLog.Completed], sink.Events.Select(value => value.MessageTemplate.Text));
            Assert.Equal("Failed", Scalar(sink.Events.Last(), "Outcome"));
            AssertSafe(sink);
        }
    }

    [Fact]
    public async Task Query_without_validator_runs_and_handler_failure_is_not_swallowed()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            var probe = services.GetRequiredService<Probe>();
            Assert.Empty(services.GetServices<IValidator<SampleQuery>>());
            Assert.Equal(42, await services.GetRequiredService<ISender>().Send(new SampleQuery()));
            var error = new InvalidOperationException("exception-secret");
            probe.Error = error;
            var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ISender>().Send(new SampleQuery()));
            Assert.Same(error, observed);
            Assert.Equal(2, probe.HandlerCalls);
            Assert.Equal("Failed", Scalar(sink.Events.Last(), "Outcome"));
            Assert.Equal(2, sink.Events.Count(value => value.MessageTemplate.Text == PipelineLog.Measured));
            AssertSafe(sink);
        }
    }

    [Fact]
    public async Task Cancellation_token_reaches_validators_and_handler_and_precanceled_request_stops()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var services = scope.ServiceProvider;
            var probe = services.GetRequiredService<Probe>();
            await services.GetRequiredService<ISender>().Send(new SampleCommand(1), cancellation.Token);
            Assert.Equal([cancellation.Token, cancellation.Token, cancellation.Token], probe.Tokens);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<ISender>().Send(new SampleQuery(), cancellation.Token));
            Assert.Equal(1, probe.HandlerCalls);
            Assert.Equal("Canceled", Scalar(sink.Events.Last(), "Outcome"));
            AssertSafe(sink);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_during_validator_or_handler_propagates(bool duringValidation)
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var services = scope.ServiceProvider;
            var probe = services.GetRequiredService<Probe>();
            probe.Cancel = cancellation;
            probe.CancelInValidation = duringValidation;
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<ISender>().Send(new SampleCommand(1), cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(duringValidation ? 0 : 1, probe.HandlerCalls);
            Assert.Equal("Canceled", Scalar(sink.Events.Last(), "Outcome"));
            Assert.Contains(sink.Events, value => value.MessageTemplate.Text == PipelineLog.Measured);
        }
    }

    [Theory]
    [InlineData("client-123_ABC", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("injected\r\nPassword: header-secret", false)]
    [InlineData("contains space", false)]
    [InlineData("عربي", false)]
    [InlineData("a,b", false)]
    public async Task Http_correlation_is_validated_and_shared_with_pipeline(string? incoming, bool accepted)
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = new DefaultHttpContext { RequestServices = services };
            if (incoming is not null) context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
            context.Request.Headers.Authorization = "Bearer authorization-secret";
            context.Request.Headers.Cookie = "cookie-secret";
            var middleware = new CorrelationIdMiddleware(async _ =>
                Assert.Equal(42, await services.GetRequiredService<ISender>().Send(new SampleQuery(), context.RequestAborted)));
            await middleware.InvokeAsync(context, services.GetRequiredService<CorrelationContext>());
            var id = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
            Assert.True(CorrelationContext.IsValid(id));
            if (accepted) Assert.Equal(incoming, id);
            else Assert.True(Guid.TryParseExact(id, "N", out _));
            Assert.All(sink.Events, value => Assert.Equal(id, Scalar(value, "CorrelationId")));
            AssertSafe(sink);
        }
    }

    [Fact]
    public async Task Concurrent_http_scopes_do_not_share_correlation_and_multiheader_is_replaced()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var arrived = 0;
            var tasks = Enumerable.Range(0, 2).Select(async index =>
            {
                using var scope = provider.CreateScope();
                var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
                context.Request.Headers[CorrelationIdMiddleware.HeaderName] = index == 0
                    ? new Microsoft.Extensions.Primitives.StringValues(["one", "two"]) : "parallel-2";
                var middleware = new CorrelationIdMiddleware(async _ =>
                {
                    if (Interlocked.Increment(ref arrived) == 2) gate.SetResult();
                    await gate.Task;
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SampleQuery());
                });
                await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<CorrelationContext>());
                return context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
            });
            var ids = await Task.WhenAll(tasks);
            Assert.NotEqual(ids[0], ids[1]);
            Assert.True(Guid.TryParseExact(ids[0], "N", out _));
            Assert.Equal("parallel-2", ids[1]);
            Assert.All(ids, id => Assert.Equal(3, sink.Events.Count(value => Equals(Scalar(value, "CorrelationId"), id))));
        }
    }

    [Fact]
    public async Task Http_middleware_preserves_exception_and_correlation_on_failure()
    {
        var context = new DefaultHttpContext();
        var error = new InvalidOperationException("exception-secret");
        var correlation = new CorrelationContext();
        var middleware = new CorrelationIdMiddleware(_ => throw error);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, correlation)));
        Assert.Equal(correlation.Id, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public void Overlong_correlation_is_replaced_and_nonhttp_fallback_is_scoped()
    {
        var (provider, _) = CreateProvider();
        using (provider)
        using (var first = provider.CreateScope())
        using (var second = provider.CreateScope())
        {
            var one = first.ServiceProvider.GetRequiredService<CorrelationContext>();
            var two = second.ServiceProvider.GetRequiredService<CorrelationContext>();
            Assert.NotEqual(one.Id, two.Id);
            one.Initialize(new string('a', 65));
            Assert.True(Guid.TryParseExact(one.Id, "N", out _));
            one.Initialize(new string('a', 64));
            Assert.Equal(new string('a', 64), one.Id);
        }
    }

    [Fact]
    public void Sink_rejects_unapproved_payloads_exceptions_and_secret_scopes()
    {
        var (provider, sink) = CreateProvider();
        using (provider)
        using (var scope = provider.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<LoggingBehavior<SampleCommand, string>>>();
            using var correlation = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "safe-id" });
            logger.LogInformation("Payload {Password} {OTP} {Tokens}", "password-secret", "otp-secret", "token-secret");
            logger.LogError(new InvalidOperationException("exception-secret"), PipelineLog.Started, "SampleCommand");
            using (logger.BeginScope(new Dictionary<string, object> { ["Authorization"] = "authorization-secret" }))
                logger.LogInformation(PipelineLog.Started, "SampleCommand");
            Assert.Empty(sink.Events);
            logger.LogInformation(PipelineLog.Started, "SampleCommand");
            Assert.Single(sink.Events);
            AssertSafe(sink);
        }
    }

    private static object? Scalar(LogEvent value, string key) => ((ScalarValue)value.Properties[key]).Value;

    private static void AssertSafe(EventSink sink)
    {
        var rendered = string.Join("\n", sink.Events.Select(value => value.RenderMessage() + string.Join(";", value.Properties)));
        foreach (var secret in new[] { "password-secret", "otp-secret", "token-secret", "cookie-secret", "authorization-secret", "result-secret", "exception-secret", "validation-secret", "header-secret" })
            Assert.DoesNotContain(secret, rendered);
        Assert.All(sink.Events, value => Assert.Null(value.Exception));
    }

    public sealed class EventSink : ILogEventSink, IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<LogEvent> events = new();
        private int disposeCount;
        public LogEvent[] Events => events.ToArray();
        public int DisposeCount => Volatile.Read(ref disposeCount);
        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
        public void Dispose() => Interlocked.Increment(ref disposeCount);
    }

    public sealed class Probe
    {
        public int HandlerCalls;
        public List<string> Trace { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public EventSink? Sink;
        public Exception? Error;
        public CancellationTokenSource? Cancel;
        public bool CancelInValidation;
    }

    public sealed record SampleCommand(int Number, string Password = "password-secret", string OTP = "otp-secret",
        string Tokens = "token-secret", string Cookies = "cookie-secret") : IRequest<string>;
    public sealed record SampleQuery : IRequest<int>;

    public sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator(Probe probe)
        {
            RuleFor(request => request.Number).MustAsync((_, token) =>
            {
                probe.Trace.Add("first-validator");
                probe.Tokens.Add(token);
                if (probe.Error is not null) throw probe.Error;
                if (probe.Sink is not null) Assert.Equal(PipelineLog.Started, probe.Sink.Events.Single().MessageTemplate.Text);
                if (probe.CancelInValidation) probe.Cancel?.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(true);
            });
            RuleFor(request => request.Number).GreaterThan(0).WithMessage("validation-secret");
        }
    }

    public sealed class SecondValidator : AbstractValidator<SampleCommand>
    {
        public SecondValidator(Probe probe)
        {
            RuleFor(request => request.Number).MustAsync((value, token) =>
            {
                probe.Trace.Add("second-validator");
                probe.Tokens.Add(token);
                return Task.FromResult(value >= 0);
            }).WithMessage("validation-secret");
        }
    }

    public sealed class SampleCommandHandler(Probe probe) : IRequestHandler<SampleCommand, string>
    {
        public Task<string> Handle(SampleCommand request, CancellationToken cancellationToken)
        {
            probe.HandlerCalls++;
            probe.Trace.Add("handler");
            probe.Tokens.Add(cancellationToken);
            if (probe.Sink is not null) Assert.Single(probe.Sink.Events);
            if (!probe.CancelInValidation) probe.Cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("result-secret");
        }
    }

    public sealed class SampleQueryHandler(Probe probe) : IRequestHandler<SampleQuery, int>
    {
        public Task<int> Handle(SampleQuery request, CancellationToken cancellationToken)
        {
            probe.HandlerCalls++;
            probe.Tokens.Add(cancellationToken);
            if (probe.Error is not null) throw probe.Error;
            return Task.FromResult(42);
        }
    }
}
