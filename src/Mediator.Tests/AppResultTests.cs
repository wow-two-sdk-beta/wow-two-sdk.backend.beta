using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests;

public sealed class AppResultTests
{
    // --- fixtures: per-operation result containers implementing the markers (the result-pattern.md shape) ---

    private sealed record SampleSuccess(string Value) : ISuccessResult;

    private sealed record SampleFailure(string Message, bool NotFound = false) : IFailureResult;

    private sealed record SampleSuccessContext(bool CacheHit) : IApplicationSuccessContext;

    private sealed record SampleFailureContext(int RetryAfterSeconds) : IApplicationFailureContext;

    private static AppResult<SampleSuccess, SampleFailure> Ok(string value, IApplicationSuccessContext? ctx = null)
        => new AppResult<SampleSuccess, SampleFailure>.Success(new SampleSuccess(value), ctx);

    private static AppResult<SampleSuccess, SampleFailure> Fail(string message, bool notFound = false, IApplicationFailureContext? ctx = null)
        => new AppResult<SampleSuccess, SampleFailure>.Failure(new SampleFailure(message, notFound), ctx);

    // --- construction ---

    [Fact]
    public void Success_carries_typed_payload()
    {
        var result = Ok("hello");

        var success = result.Should().BeOfType<AppResult<SampleSuccess, SampleFailure>.Success>().Subject;
        success.Data.Value.Should().Be("hello");
        success.Context.Should().BeNull();
    }

    [Fact]
    public void Failure_carries_typed_error()
    {
        var result = Fail("boom", notFound: true);

        var failure = result.Should().BeOfType<AppResult<SampleSuccess, SampleFailure>.Failure>().Subject;
        failure.Error.Message.Should().Be("boom");
        failure.Error.NotFound.Should().BeTrue();
        failure.Context.Should().BeNull();
    }

    [Fact]
    public void Success_carries_optional_context()
    {
        var result = Ok("hello", new SampleSuccessContext(CacheHit: true));

        var success = (AppResult<SampleSuccess, SampleFailure>.Success)result;
        success.Context.Should().BeOfType<SampleSuccessContext>()
            .Which.CacheHit.Should().BeTrue();
    }

    [Fact]
    public void Failure_carries_optional_context()
    {
        var result = Fail("rate limited", ctx: new SampleFailureContext(RetryAfterSeconds: 30));

        var failure = (AppResult<SampleSuccess, SampleFailure>.Failure)result;
        failure.Context.Should().BeOfType<SampleFailureContext>()
            .Which.RetryAfterSeconds.Should().Be(30);
    }

    // --- Match: case-object overload (planning-spec canonical shape) ---

    [Fact]
    public void Match_success_arm_receives_success_case()
    {
        var result = Ok("payload");

        var output = result.Match(
            onSuccess: s => $"ok:{s.Data.Value}",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("ok:payload");
    }

    [Fact]
    public void Match_failure_arm_receives_failure_case()
    {
        var result = Fail("not found", notFound: true);

        var status = result.Match(
            onSuccess: _ => 200,
            onFailure: f => f.Error.NotFound ? 404 : 500);

        status.Should().Be(404);
    }

    [Fact]
    public void Match_unifies_both_arms_to_one_out_type()
    {
        AppResult<SampleSuccess, SampleFailure> ok = Ok("a");
        AppResult<SampleSuccess, SampleFailure> fail = Fail("b");

        ok.Match(s => s.Data.Value.Length, _ => -1).Should().Be(1);
        fail.Match(_ => 99, f => f.Error.Message.Length).Should().Be(1);
    }

    // --- Match: no-arg success overload (void-ish commands → NoContent) ---

    [Fact]
    public void Match_no_arg_success_arm_ignores_payload()
    {
        var result = Ok("irrelevant");

        var output = result.Match(
            onSuccess: () => "no-content",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("no-content");
    }

    [Fact]
    public void Match_no_arg_success_overload_still_routes_failures()
    {
        var result = Fail("denied");

        var output = result.Match(
            onSuccess: () => "no-content",
            onFailure: f => $"err:{f.Error.Message}");

        output.Should().Be("err:denied");
    }

    // --- guards ---

    [Fact]
    public void Match_throws_on_null_success_delegate()
    {
        var result = Ok("x");
        var act = () => result.Match(onSuccess: (Func<AppResult<SampleSuccess, SampleFailure>.Success, int>)null!, onFailure: _ => 0);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Match_throws_on_null_failure_delegate()
    {
        var result = Ok("x");
        var act = () => result.Match(onSuccess: _ => 0, onFailure: (Func<AppResult<SampleSuccess, SampleFailure>.Failure, int>)null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
