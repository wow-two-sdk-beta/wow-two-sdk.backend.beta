using AwesomeAssertions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Logging;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="LoggingInterceptor{TRequest,TResponse}"/> — opens a module span and logs start + completion on success;
/// a propagating failure is left to its handling boundary.
/// </summary>
public sealed class LoggingBehaviorTests
{
    private sealed record Req(string V) : IRequest<string>;

    // Minimal ILogger capturing (level, eventId) pairs — enough to assert what was logged.
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, int EventId)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, eventId.Id));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task HandleAsync_ShouldLogStartAndCompletionAndReturnResult_WhenSuccess()
    {
        var logger = new CapturingLogger<LoggingInterceptor<Req, string>>();
        var behavior = new LoggingInterceptor<Req, string>(logger);

        var result = await behavior.HandleAsync(new Req("x"), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok"); // behavior is transparent to the response
        logger.Entries.Should().Contain(e => e.EventId == 1001); // start (Information)
        logger.Entries.Should().Contain(e => e.EventId == 1002 && e.Level == LogLevel.Information); // completed
        logger.Entries.Should().NotContain(e => e.EventId == 1003); // no failure log
    }

    [Fact]
    public async Task HandleAsync_ShouldRethrowWithoutLoggingTheFailure_WhenHandlerThrows()
    {
        var logger = new CapturingLogger<LoggingInterceptor<Req, string>>();
        var behavior = new LoggingInterceptor<Req, string>(logger);

        var act = async () => await behavior.HandleAsync(
            new Req("x"),
            () => throw new InvalidOperationException("kaboom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("kaboom");
        logger.Entries.Should().Contain(e => e.EventId == 1001); // start
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error); // the handling boundary records once
        logger.Entries.Should().NotContain(e => e.EventId == 1002); // never completed
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateMediatorActivity_WhenListenerIsEnabled()
    {
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "WoW.Two.Mediator",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new LoggingInterceptor<Req, string>(new CapturingLogger<LoggingInterceptor<Req, string>>());

        await behavior.HandleAsync(new Req("x"), () => ValueTask.FromResult("ok"), CancellationToken.None);

        stopped.Should().NotBeNull();
        stopped!.Source.Name.Should().Be("WoW.Two.Mediator");
        stopped.Kind.Should().Be(ActivityKind.Internal);
        stopped.GetTagItem("request.type").Should().Be(typeof(Req).FullName);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenNextNull()
    {
        var logger = new CapturingLogger<LoggingInterceptor<Req, string>>();
        var behavior = new LoggingInterceptor<Req, string>(logger);

        var act = async () => await behavior.HandleAsync(new Req("x"), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
