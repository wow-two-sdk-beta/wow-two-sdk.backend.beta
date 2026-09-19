using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors.Mappers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class ErrorNatureTests
{
    private readonly ErrorNatureMapper _mapper = new();

    [Theory]
    [InlineData(AppErrorType.DbTimeout)]
    [InlineData(AppErrorType.OperationTimeout)]
    [InlineData(AppErrorType.ExternalUnavailable)]
    [InlineData(AppErrorType.TooManyRequests)]
    public void Map_ShouldBeTransient_WhenTransientKind(AppErrorType type)
    {
        _mapper.Map(type).Should().Be(ErrorNature.Transient);
    }

    [Theory]
    [InlineData(AppErrorType.Unexpected)]
    [InlineData(AppErrorType.SerializationFailed)]
    [InlineData(AppErrorType.FileNotFound)]
    [InlineData(AppErrorType.DataIntegrity)]
    public void Map_ShouldBeDefect_WhenDefectKind(AppErrorType type)
    {
        _mapper.Map(type).Should().Be(ErrorNature.Defect);
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
    public void Map_ShouldBePermanent_WhenPermanentKind(AppErrorType type)
    {
        _mapper.Map(type).Should().Be(ErrorNature.Permanent);
    }
}
