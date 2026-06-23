using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class AppErrorTests
{
    [Fact]
    public void Of_ShouldSetTypeAndMessage()
    {
        var error = AppError.Of(AppErrorType.NotFound, "missing");

        error.Type.Should().Be(AppErrorType.NotFound);
        error.Message.Should().Be("missing");
    }

    [Fact]
    public void Of_ShouldCaptureCallingMemberIntoOrigin()
    {
        var error = AppError.Of(AppErrorType.Conflict, "dup");

        error.Origin.Should().NotBeNull();
        error.Origin!.Member.Should().Be(nameof(Of_ShouldCaptureCallingMemberIntoOrigin));
    }

    [Fact]
    public void Of_ShouldThrow_WhenMessageBlank()
    {
        var act = () => AppError.Of(AppErrorType.Unexpected, "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromException_ShouldRecordCauseAndChainInMetadata()
    {
        var inner = new InvalidOperationException("kaboom");

        var error = AppError.FromException(AppErrorType.Unexpected, "wrapped", inner);

        error.Metadata.Should().NotBeNull();
        error.Metadata!["cause"].Should().Be(nameof(InvalidOperationException));
        error.Metadata!["causeChain"].Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public void FromException_ShouldDeriveOriginFromThrowSite()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        var error = AppError.FromException(AppErrorType.Unexpected, "wrapped", caught);

        error.Origin.Should().NotBeNull();
        error.Origin!.Member.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FromException_ShouldThrow_WhenExceptionNull()
    {
        var act = () => AppError.FromException(AppErrorType.Unexpected, "msg", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Is_ShouldReturnTrue_WhenTypeMatches()
    {
        var error = AppError.Of(AppErrorType.Forbidden, "no");

        error.Is(AppErrorType.Forbidden).Should().BeTrue();
        error.Is(AppErrorType.NotFound).Should().BeFalse();
    }

    [Fact]
    public void ToException_ShouldWrapErrorAndPreserveInner()
    {
        var inner = new InvalidOperationException("cause");
        var error = AppError.Of(AppErrorType.Unexpected, "boom");

        var exception = error.ToException(inner);

        exception.Error.Should().BeSameAs(error);
        exception.InnerException.Should().BeSameAs(inner);
        exception.Message.Should().Be("boom");
    }

    [Fact]
    public void Throw_ShouldRaiseAppExceptionCarryingTheError()
    {
        var error = AppError.Of(AppErrorType.NotFound, "gone");

        var act = () => error.Throw();

        act.Should().Throw<AppException>()
            .Which.Error.Should().BeSameAs(error);
    }
}
