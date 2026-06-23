using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Logging;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="LoggingBehavior{TRequest,TResponse}"/> — wraps the handler, logs start + completion on success,
/// logs an error and rethrows on failure.
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
        var logger = new CapturingLogger<LoggingBehavior<Req, string>>();
        var behavior = new LoggingBehavior<Req, string>(logger);

        var result = await behavior.HandleAsync(new Req("x"), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok"); // behavior is transparent to the response
        logger.Entries.Should().Contain(e => e.EventId == 1001); // start (Information)
        logger.Entries.Should().Contain(e => e.EventId == 1002 && e.Level == LogLevel.Information); // completed
        logger.Entries.Should().NotContain(e => e.EventId == 1003); // no failure log
    }

    [Fact]
    public async Task HandleAsync_ShouldLogFailureAndRethrow_WhenHandlerThrows()
    {
        var logger = new CapturingLogger<LoggingBehavior<Req, string>>();
        var behavior = new LoggingBehavior<Req, string>(logger);

        var act = async () => await behavior.HandleAsync(
            new Req("x"),
            () => throw new InvalidOperationException("kaboom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("kaboom");
        logger.Entries.Should().Contain(e => e.EventId == 1001);                              // start
        logger.Entries.Should().Contain(e => e.EventId == 1003 && e.Level == LogLevel.Error); // failure (Error)
        logger.Entries.Should().NotContain(e => e.EventId == 1002);                           // never completed
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenNextNull()
    {
        var logger = new CapturingLogger<LoggingBehavior<Req, string>>();
        var behavior = new LoggingBehavior<Req, string>(logger);

        var act = async () => await behavior.HandleAsync(new Req("x"), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
