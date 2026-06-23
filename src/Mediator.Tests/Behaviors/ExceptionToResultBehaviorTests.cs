using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.ExceptionHandling;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using WoW.Two.Sdk.Backend.Beta.Observability.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="ExceptionToResultBehavior{TRequest,TResponse}"/> — converts handler throws into an
/// <c>AppResult.Failure</c> for AppResult-returning requests; OCE maps to Canceled/OperationTimeout;
/// non-AppResult responses rethrow as an <see cref="AppException"/>.
/// </summary>
public sealed class ExceptionToResultBehaviorTests
{
    private sealed record Payload(string Value);

    private sealed record Req : IRequest<AppResult<Payload>>;

    private sealed record PlainReq : IRequest<string>;

    private static AppErrorObserver Observer()
        => new(NullLogger<AppErrorObserver>.Instance, new DefaultErrorNatureClassifier());

    private static ExceptionToResultBehavior<TReq, TResp> Behavior<TReq, TResp>()
        where TReq : notnull
        => new(Observer(), new ExceptionMapper([]));

    [Fact]
    public async Task HandleAsync_ShouldPassThroughSuccessfulResult()
    {
        var behavior = Behavior<Req, AppResult<Payload>>();
        var expected = AppResult<Payload>.Ok(new Payload("ok"));

        var result = await behavior.HandleAsync(new Req(), () => ValueTask.FromResult(expected), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAsync_ShouldConvertAppExceptionToFailure()
    {
        var behavior = Behavior<Req, AppResult<Payload>>();
        var error = AppErrors.NotFound("missing");

        var result = await behavior.HandleAsync(
            new Req(),
            () => throw error.ToException(),
            CancellationToken.None);

        result.Should().BeOfType<AppResult<Payload>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_ShouldConvertUnexpectedExceptionToUnexpectedFailure()
    {
        var behavior = Behavior<Req, AppResult<Payload>>();

        var result = await behavior.HandleAsync(
            new Req(),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        result.Should().BeOfType<AppResult<Payload>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Unexpected);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapToCanceled_WhenRequestCancellation()
    {
        var behavior = Behavior<Req, AppResult<Payload>>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await behavior.HandleAsync(
            new Req(),
            () => throw new OperationCanceledException(cts.Token),
            cts.Token);

        result.Should().BeOfType<AppResult<Payload>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Canceled);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapToOperationTimeout_WhenInternalDeadlineCancellation()
    {
        var behavior = Behavior<Req, AppResult<Payload>>();

        // The request token is NOT cancelled → an OCE means an internal deadline → 504.
        var result = await behavior.HandleAsync(
            new Req(),
            () => throw new OperationCanceledException(),
            CancellationToken.None);

        result.Should().BeOfType<AppResult<Payload>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.OperationTimeout);
    }

    [Fact]
    public async Task HandleAsync_ShouldRethrowAsAppException_WhenNonAppResultResponse()
    {
        var behavior = Behavior<PlainReq, string>();

        var act = async () => await behavior.HandleAsync(
            new PlainReq(),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AppException>();
    }
}
