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
        { () => AppErrorFactory.NotFound(), AppErrorType.NotFound },
        { () => AppErrorFactory.Conflict(), AppErrorType.Conflict },
        { () => AppErrorFactory.BusinessRule(), AppErrorType.BusinessRule },
        { () => AppErrorFactory.PaymentRequired(), AppErrorType.PaymentRequired },
        { () => AppErrorFactory.Gone(), AppErrorType.Gone },
        { () => AppErrorFactory.Forbidden(), AppErrorType.Forbidden },
        { () => AppErrorFactory.Unauthorized(), AppErrorType.Unauthorized },
        { () => AppErrorFactory.Validation(), AppErrorType.Validation },
        { () => AppErrorFactory.TooManyRequests(), AppErrorType.TooManyRequests },
        { () => AppErrorFactory.DbTimeout(), AppErrorType.DbTimeout },
        { () => AppErrorFactory.OperationTimeout(), AppErrorType.OperationTimeout },
        { () => AppErrorFactory.ExternalUnavailable(), AppErrorType.ExternalUnavailable },
        { () => AppErrorFactory.ExternalUnauthorized(), AppErrorType.ExternalUnauthorized },
        { () => AppErrorFactory.FileNotFound(), AppErrorType.FileNotFound },
        { () => AppErrorFactory.DataIntegrity(), AppErrorType.DataIntegrity },
        { () => AppErrorFactory.Canceled(), AppErrorType.Canceled },
        { () => AppErrorFactory.Unexpected(), AppErrorType.Unexpected },
    };

    [Fact]
    public void Unexpected_ShouldFoldInCause_WhenInnerProvided()
    {
        var inner = new InvalidOperationException("boom");

        var error = AppErrorFactory.Unexpected(inner: inner);

        error.Type.Should().Be(AppErrorType.Unexpected);
        error.Metadata.Should().NotBeNull();
        error.Metadata!["cause"].Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void Unexpected_ShouldCaptureCallSite_WhenNoInner()
    {
        var error = AppErrorFactory.Unexpected();

        error.Origin!.Member.Should().Be(nameof(Unexpected_ShouldCaptureCallSite_WhenNoInner));
    }

    [Fact]
    public void DataIntegrity_ShouldFoldInCause_WhenInnerProvided()
    {
        var inner = new InvalidOperationException("corrupt");

        var error = AppErrorFactory.DataIntegrity(inner: inner);

        error.Type.Should().Be(AppErrorType.DataIntegrity);
        error.Metadata!["cause"].Should().Be(nameof(InvalidOperationException));
    }
}
