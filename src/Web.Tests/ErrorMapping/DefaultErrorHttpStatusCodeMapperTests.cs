using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ErrorMapping;

public sealed class DefaultErrorHttpStatusCodeMapperTests
{
    private readonly DefaultErrorHttpStatusCodeMapper _mapper = new();

    [Theory]
    [InlineData(AppErrorType.Validation, 400)]
    [InlineData(AppErrorType.Unauthorized, 401)]
    [InlineData(AppErrorType.ExternalUnauthorized, 401)]
    [InlineData(AppErrorType.Forbidden, 403)]
    [InlineData(AppErrorType.NotFound, 404)]
    [InlineData(AppErrorType.FileNotFound, 404)]
    [InlineData(AppErrorType.Conflict, 409)]
    [InlineData(AppErrorType.BusinessRule, 422)]
    [InlineData(AppErrorType.PaymentRequired, 402)]
    [InlineData(AppErrorType.Gone, 410)]
    [InlineData(AppErrorType.TooManyRequests, 429)]
    [InlineData(AppErrorType.ExternalUnavailable, 503)]
    [InlineData(AppErrorType.DbTimeout, 504)]
    [InlineData(AppErrorType.OperationTimeout, 504)]
    [InlineData(AppErrorType.Canceled, 499)]
    [InlineData(AppErrorType.DataIntegrity, 500)]
    [InlineData(AppErrorType.SerializationFailed, 500)]
    [InlineData(AppErrorType.Unexpected, 500)]
    public void ToStatusCode_ShouldMapEachKind(AppErrorType type, int expected)
    {
        var status = _mapper.ToStatusCode(AppError.Of(type, "msg"));

        status.Should().Be(expected);
    }

    [Fact]
    public void ToStatusCode_ShouldThrow_WhenNull()
    {
        var act = () => _mapper.ToStatusCode(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
