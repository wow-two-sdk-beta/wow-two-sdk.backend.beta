using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

public sealed class AppResultTests
{
    // --- fixtures: the collapsed AppResult<TSuccess> shape (failure is always an AppError) ---

    private sealed record SampleSuccess(string Value);

    private sealed record SampleSuccessContext(bool CacheHit) : IAppSuccessContext;

    private sealed record SampleFailureContext(int RetryAfterSeconds) : IAppFailureContext;

    private static AppResult<SampleSuccess> Ok(string value, IAppSuccessContext? ctx = null)
        => new AppResult<SampleSuccess>.Success { Data = new SampleSuccess(value), Context = ctx };

    private static AppResult<SampleSuccess> Fail(AppErrorType type = AppErrorType.Unexpected, string message = "boom", IAppFailureContext? ctx = null)
        => new AppResult<SampleSuccess>.Failure { Error = AppError.Of(type, message), Context = ctx };

    // --- construction ---

    [Fact]
    public void Success_ShouldCarryTypedPayload()
    {
        var result = Ok("hello");

        var success = result.Should().BeOfType<AppResult<SampleSuccess>.Success>().Subject;
        success.Data.Value.Should().Be("hello");
        success.Context.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCarryAppError()
    {
        var result = Fail(AppErrorType.NotFound, "missing");

        var failure = result.Should().BeOfType<AppResult<SampleSuccess>.Failure>().Subject;
        failure.Error.Message.Should().Be("missing");
        failure.Error.Type.Should().Be(AppErrorType.NotFound);
        failure.Context.Should().BeNull();
    }

    [Fact]
    public void Ok_ShouldBuildSuccess()
    {
        var result = AppResult<SampleSuccess>.Ok(new SampleSuccess("data"));

        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<AppResult<SampleSuccess>.Success>()
            .Which.Data.Value.Should().Be("data");
    }

    [Fact]
    public void Fail_ShouldBuildFailure()
    {
        var result = AppResult<SampleSuccess>.Fail(AppErrors.Conflict("dup"));

        result.IsSuccess.Should().BeFalse();
        result.Should().BeOfType<AppResult<SampleSuccess>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.Conflict);
    }

    [Fact]
    public void Success_ShouldCarryOptionalContext()
    {
        var result = Ok("hello", new SampleSuccessContext(CacheHit: true));

        var success = (AppResult<SampleSuccess>.Success)result;
        success.Context.Should().BeOfType<SampleSuccessContext>()
            .Which.CacheHit.Should().BeTrue();
    }

    [Fact]
    public void Failure_ShouldCarryOptionalContext()
    {
        var result = Fail(AppErrorType.TooManyRequests, "rate limited", ctx: new SampleFailureContext(RetryAfterSeconds: 30));

        var failure = (AppResult<SampleSuccess>.Failure)result;
        failure.Context.Should().BeOfType<SampleFailureContext>()
            .Which.RetryAfterSeconds.Should().Be(30);
    }

    // --- Match: case-object overload ---

    [Fact]
    public void Match_ShouldRouteToSuccessArm_WhenSuccess()
    {
        var result = Ok("payload");

        var output = result.Match(
            onSuccess: s => $"ok:{s.Data.Value}",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("ok:payload");
    }

    [Fact]
    public void Match_ShouldRouteToFailureArm_WhenFailure()
    {
        var result = Fail(AppErrorType.NotFound, "not found");

        var status = result.Match(
            onSuccess: _ => 200,
            onFailure: f => f.Error.Is(AppErrorType.NotFound) ? 404 : 500);

        status.Should().Be(404);
    }

    [Fact]
    public void Match_ShouldUnifyBothArmsToOneOutType()
    {
        AppResult<SampleSuccess> ok = Ok("a");
        AppResult<SampleSuccess> fail = Fail(message: "b");

        ok.Match(s => s.Data.Value.Length, _ => -1).Should().Be(1);
        fail.Match(_ => 99, f => f.Error.Message.Length).Should().Be(1);
    }

    // --- Match: no-arg success overload (void-ish commands → NoContent) ---

    [Fact]
    public void Match_ShouldIgnorePayload_WhenNoArgSuccessArm()
    {
        var result = Ok("irrelevant");

        var output = result.Match(
            onSuccess: () => "no-content",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("no-content");
    }

    [Fact]
    public void Match_ShouldStillRouteFailures_WhenNoArgSuccessOverload()
    {
        var result = Fail(message: "denied");

        var output = result.Match(
            onSuccess: () => "no-content",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("err:denied");
    }

    // --- guards ---

    [Fact]
    public void Match_ShouldThrow_WhenSuccessDelegateNull()
    {
        var result = Ok("x");
        var act = () => result.Match(onSuccess: (Func<AppResult<SampleSuccess>.Success, int>)null!, onFailure: _ => 0);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Match_ShouldThrow_WhenFailureDelegateNull()
    {
        var result = Ok("x");
        var act = () => result.Match(onSuccess: _ => 0, onFailure: (Func<AppResult<SampleSuccess>.Failure, int>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
