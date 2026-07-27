using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Results;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void ValueOrThrow_ShouldReturnValue_WhenSuccess()
    {
        var value = Result<int>.Ok(99).ValueOrThrow();

        value.Should().Be(99);
    }

    [Fact]
    public void ValueOrThrow_ShouldThrowAppException_WhenFailure()
    {
        var result = Result<int>.Fail(AppErrors.NotFound("gone"));

        var act = () => result.ValueOrThrow();

        act.Should().Throw<AppException>()
            .Which.Error.Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public void ThrowIfFailure_ShouldDoNothing_WhenSuccess()
    {
        var act = () => Result.Ok().ThrowIfFailure();

        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfFailure_ShouldThrow_WhenFailure()
    {
        var result = Result.Fail(AppErrors.Conflict("dup"));

        var act = () => result.ThrowIfFailure();

        act.Should().Throw<AppException>()
            .Which.Error.Type.Should().Be(AppErrorType.Conflict);
    }
}
