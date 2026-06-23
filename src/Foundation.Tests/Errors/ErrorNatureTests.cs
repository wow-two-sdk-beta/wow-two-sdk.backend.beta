using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class ErrorNatureTests
{
    private readonly DefaultErrorNatureClassifier _classifier = new();

    [Theory]
    [InlineData(AppErrorType.DbTimeout)]
    [InlineData(AppErrorType.OperationTimeout)]
    [InlineData(AppErrorType.ExternalUnavailable)]
    [InlineData(AppErrorType.TooManyRequests)]
    public void Classify_ShouldBeTransient_WhenTransientKind(AppErrorType type)
    {
        _classifier.Classify(type).Should().Be(ErrorNature.Transient);
    }

    [Theory]
    [InlineData(AppErrorType.Unexpected)]
    [InlineData(AppErrorType.SerializationFailed)]
    [InlineData(AppErrorType.FileNotFound)]
    [InlineData(AppErrorType.DataIntegrity)]
    public void Classify_ShouldBeDefect_WhenDefectKind(AppErrorType type)
    {
        _classifier.Classify(type).Should().Be(ErrorNature.Defect);
    }

    [Theory]
    [InlineData(AppErrorType.Validation)]
    [InlineData(AppErrorType.NotFound)]
    [InlineData(AppErrorType.Unauthorized)]
    [InlineData(AppErrorType.Forbidden)]
    [InlineData(AppErrorType.Conflict)]
    [InlineData(AppErrorType.BusinessRule)]
    [InlineData(AppErrorType.PaymentRequired)]
    [InlineData(AppErrorType.Gone)]
    public void Classify_ShouldBePermanent_WhenPermanentKind(AppErrorType type)
    {
        _classifier.Classify(type).Should().Be(ErrorNature.Permanent);
    }
}
