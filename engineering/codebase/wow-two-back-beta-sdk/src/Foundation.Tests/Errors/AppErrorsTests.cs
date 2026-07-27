using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class AppErrorsTests
{
    [Theory]
    [MemberData(nameof(SimpleFactories))]
    public void Factory_ShouldProduceExpectedType(Func<AppError> factory, AppErrorType expected)
    {
        var error = factory();

        error.Type.Should().Be(expected);
        error.Message.Should().NotBeNullOrWhiteSpace();
        error.Origin.Should().NotBeNull();
    }

    public static TheoryData<Func<AppError>, AppErrorType> SimpleFactories => new()
    {
        { () => AppErrors.NotFound(), AppErrorType.NotFound },
        { () => AppErrors.Conflict(), AppErrorType.Conflict },
        { () => AppErrors.BusinessRule(), AppErrorType.BusinessRule },
        { () => AppErrors.PaymentRequired(), AppErrorType.PaymentRequired },
        { () => AppErrors.Gone(), AppErrorType.Gone },
        { () => AppErrors.Forbidden(), AppErrorType.Forbidden },
        { () => AppErrors.Unauthorized(), AppErrorType.Unauthorized },
        { () => AppErrors.Validation(), AppErrorType.Validation },
        { () => AppErrors.TooManyRequests(), AppErrorType.TooManyRequests },
        { () => AppErrors.DbTimeout(), AppErrorType.DbTimeout },
        { () => AppErrors.OperationTimeout(), AppErrorType.OperationTimeout },
        { () => AppErrors.ExternalUnavailable(), AppErrorType.ExternalUnavailable },
        { () => AppErrors.ExternalUnauthorized(), AppErrorType.ExternalUnauthorized },
        { () => AppErrors.FileNotFound(), AppErrorType.FileNotFound },
        { () => AppErrors.DataIntegrity(), AppErrorType.DataIntegrity },
        { () => AppErrors.Canceled(), AppErrorType.Canceled },
        { () => AppErrors.Unexpected(), AppErrorType.Unexpected },
    };

    [Fact]
    public void Unexpected_ShouldFoldInCause_WhenInnerProvided()
    {
        var inner = new InvalidOperationException("boom");

        var error = AppErrors.Unexpected(inner: inner);

        error.Type.Should().Be(AppErrorType.Unexpected);
        error.Metadata.Should().NotBeNull();
        error.Metadata!["cause"].Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void Unexpected_ShouldCaptureCallSite_WhenNoInner()
    {
        var error = AppErrors.Unexpected();

        error.Origin!.Member.Should().Be(nameof(Unexpected_ShouldCaptureCallSite_WhenNoInner));
    }

    [Fact]
    public void DataIntegrity_ShouldFoldInCause_WhenInnerProvided()
    {
        var inner = new InvalidOperationException("corrupt");

        var error = AppErrors.DataIntegrity(inner: inner);

        error.Type.Should().Be(AppErrorType.DataIntegrity);
        error.Metadata!["cause"].Should().Be(nameof(InvalidOperationException));
    }
}
