using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Results;

public sealed class AttemptExtensionsTests
{
    [Fact]
    public void Attempt_ShouldCaptureSuccess()
    {
        var result = new Func<int>(() => 5).Attempt();

        result.Should().BeOfType<Result<int>.Success>()
            .Which.Value.Should().Be(5);
    }

    [Fact]
    public void Attempt_ShouldMapToUnexpectedError_WhenUnexpectedException()
    {
        var result = new Func<int>(() => throw new InvalidOperationException("boom")).Attempt();

        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Unexpected);
    }

    [Fact]
    public void Attempt_ShouldPreserveAppExceptionError()
    {
        var error = AppErrors.NotFound("missing");
        var result = new Func<int>(() => throw error.ToException()).Attempt();

        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Attempt_ShouldReturnCanceled_WhenOperationCanceled()
    {
        var result = new Func<int>(() => throw new OperationCanceledException()).Attempt();

        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Canceled);
    }

    [Fact]
    public async Task AttemptAsync_ShouldCaptureSuccess()
    {
        var result = await new Func<Task<int>>(() => Task.FromResult(8)).AttemptAsync();

        result.Should().BeOfType<Result<int>.Success>()
            .Which.Value.Should().Be(8);
    }

    [Fact]
    public async Task AttemptAsync_ShouldMapToUnexpectedError_WhenUnexpectedException()
    {
        var result = await new Func<Task<int>>(() => throw new InvalidOperationException("boom")).AttemptAsync();

        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Unexpected);
    }

    [Fact]
    public async Task AttemptAsync_ShouldReturnCanceled_WhenOperationCanceled()
    {
        var result = await new Func<Task<int>>(() => throw new OperationCanceledException()).AttemptAsync();

        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Canceled);
    }
}
