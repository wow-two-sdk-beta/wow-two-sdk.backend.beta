using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Ok_ShouldBuildSuccess()
    {
        var result = Result<int>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Result<int>.Success>()
            .Which.Value.Should().Be(42);
    }

    [Fact]
    public void Fail_ShouldBuildFailureCarryingAppError()
    {
        var result = Result<int>.Fail(AppErrors.NotFound("nope"));

        result.IsSuccess.Should().BeFalse();
        result.Should().BeOfType<Result<int>.Failure>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public void Match_ShouldRouteSuccessAndFailure()
    {
        Result<int>.Ok(7).Match(v => v * 2, _ => -1).Should().Be(14);
        Result<int>.Fail(AppErrors.Conflict("x")).Match(v => v, _ => -1).Should().Be(-1);
    }

    [Fact]
    public void Map_ShouldTransformSuccessValue()
    {
        var mapped = Result<int>.Ok(3).Map(v => $"n={v}");

        mapped.Should().BeOfType<Result<string>.Success>()
            .Which.Value.Should().Be("n=3");
    }

    [Fact]
    public void Map_ShouldPropagateFailureUnchanged()
    {
        var error = AppErrors.Conflict("dup");

        var mapped = Result<int>.Fail(error).Map(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));

        mapped.Should().BeOfType<Result<string>.Failure>()
            .Which.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void NonGeneric_ShouldBuildOkAndFail()
    {
        Result.Ok().IsSuccess.Should().BeTrue();
        Result.Fail(AppErrors.Unexpected()).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Match_ShouldThrow_WhenDelegatesNull()
    {
        var act = () => Result<int>.Ok(1).Match(null!, _ => 0);

        act.Should().Throw<ArgumentNullException>();
    }
}
