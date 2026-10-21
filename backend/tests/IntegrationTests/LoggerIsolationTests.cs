using System.Net;
using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Application.Shared.Diagnostics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace HawalekNow.IntegrationTests;

public sealed class LoggerIsolationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Independent_instances_keep_their_events_and_own_logger_lifetime(bool firstHttp, bool secondHttp)
    {
        var staticLogger = Log.Logger;
        await using var first = await Instance.StartAsync(firstHttp);
        await using var second = await Instance.StartAsync(secondHttp);
        // Both factories exist before resolving any pipeline loggers: detects late binding to the static logger.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSend = Task.Run(async () => { await release.Task; await first.SendAsync("isolation-first"); });
        var secondSend = Task.Run(async () => { await release.Task; await second.SendAsync("isolation-second"); });
        release.SetResult();
        await Task.WhenAll(firstSend, secondSend);

        output.WriteLine("First sink: {0} events, correlations: {1}", first.Sink.Events.Length, Correlations(first.Sink));
        output.WriteLine("Second sink: {0} events, correlations: {1}", second.Sink.Events.Length, Correlations(second.Sink));
        output.WriteLine("Static logger preserved: {0}", ReferenceEquals(staticLogger, Log.Logger));
        AssertEvents(first.Sink, "isolation-first", firstHttp, 1);
        AssertEvents(second.Sink, "isolation-second", secondHttp, 1);
        Assert.Same(staticLogger, Log.Logger);
        Assert.Equal(0, first.Sink.DisposeCount);
        Assert.Equal(0, second.Sink.DisposeCount);

        await first.DisposeAsync();
        Assert.Equal(1, first.Sink.DisposeCount);
        Assert.Equal(0, second.Sink.DisposeCount);
        await second.SendAsync("isolation-second");
        AssertEvents(first.Sink, "isolation-first", firstHttp, 1);
        AssertEvents(second.Sink, "isolation-second", secondHttp, 2);
        await second.DisposeAsync();
        Assert.Equal(1, second.Sink.DisposeCount);
        Assert.Equal(1, first.Sink.DisposeCount);
        Assert.Same(staticLogger, Log.Logger);
    }

    private static string Correlations(PipelineTests.EventSink sink) =>
        string.Join(",", sink.Events.Select(entry => ((ScalarValue)entry.Properties["CorrelationId"]).Value));

    private static void AssertEvents(PipelineTests.EventSink sink, string correlation, bool http, int sends)
    {
        var events = sink.Events;
        Assert.Equal(sends * 3, events.Length);
        Assert.Equal(Enumerable.Range(0, sends).SelectMany(_ =>
            new[] { PipelineLog.Started, PipelineLog.Measured, PipelineLog.Completed }),
            events.Select(entry => entry.MessageTemplate.Text));
        Assert.All(events, entry =>
        {
            Assert.Equal(correlation, ((ScalarValue)entry.Properties["CorrelationId"]).Value);
            Assert.Equal(http ? "ExpectedCommand" : "SampleQuery", ((ScalarValue)entry.Properties["RequestType"]).Value);
            Assert.Null(entry.Exception);
        });
    }

    private sealed class Instance(PipelineTests.EventSink sink, Func<string, Task> send, Func<ValueTask> dispose)
        : IAsyncDisposable
    {
        private bool disposed;
        public PipelineTests.EventSink Sink { get; } = sink;
        public Task SendAsync(string correlation) => send(correlation);

        public static async Task<Instance> StartAsync(bool http)
        {
            if (http)
            {
                var (app, client, sink) = await ApiErrorTests.StartAsync();
                return new Instance(sink, async correlation =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/result/NotFound");
                    request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlation);
                    using var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                    Assert.Equal(correlation, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
                }, async () => { client.Dispose(); await app.DisposeAsync(); });
            }

            var (provider, providerSink) = PipelineTests.CreateProvider();
            _ = provider.GetRequiredService<ILoggerFactory>();
            return new Instance(providerSink, async correlation =>
            {
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<CorrelationContext>().Initialize(correlation);
                Assert.Equal(42, await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PipelineTests.SampleQuery()));
            }, () => { provider.Dispose(); return ValueTask.CompletedTask; });
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            await dispose();
        }
    }
}
