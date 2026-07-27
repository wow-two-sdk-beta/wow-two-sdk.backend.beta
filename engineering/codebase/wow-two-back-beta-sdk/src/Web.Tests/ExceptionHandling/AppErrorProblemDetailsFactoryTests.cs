using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ExceptionHandling;

public sealed class AppErrorProblemDetailsFactoryTests
{
    private static readonly IErrorHttpStatusCodeMapper Mapper = new DefaultErrorHttpStatusCodeMapper();
    private static readonly IErrorMessageResolver Resolver = new DefaultErrorMessageResolver();

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, out HttpContext context)
    {
        context = new DefaultHttpContext();
        return AppErrorProblemDetailsFactory.Create(error, context, Mapper, Resolver);
    }

    [Fact]
    public void Create_ShouldSetStatusTypeCodeAndDetail()
    {
        var problem = Create(AppErrors.NotFound("Order 3f2 not found."), out var context);

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Type.Should().Be("urn:wow-two:error:NotFound");
        problem.Detail.Should().Be("Order 3f2 not found.");
        problem.Extensions["code"].Should().Be("NotFound");
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Create_ShouldEmitErrors_WhenValidationError()
    {
        var error = ValidationError.From(
        [
            new FieldError { Property = "email", Message = "Email is required.", Code = "NotEmptyValidator" },
        ]);

        var problem = Create(error, out _);

        problem.Extensions.Should().ContainKey("errors");
        problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<FieldError>>()
            .Which.Should().ContainSingle(f => f.Property == "email");
    }

    [Fact]
    public void Create_ShouldEmitErrors_WhenAggregateOfValidationErrors()
    {
        var inner = ValidationError.From([new FieldError { Property = "name", Message = "required", Code = "rule" }]);
        var aggregate = AppAggregateError.From([inner]);

        var problem = Create(aggregate, out _);

        problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<FieldError>>()
            .Which.Should().ContainSingle(f => f.Property == "name");
    }

    [Fact]
    public void Create_ShouldPromoteReservedMetadataToResponseHeaders()
    {
        var error = AppError.Of(
            AppErrorType.TooManyRequests,
            "slow down",
            new Dictionary<string, object?>
            {
                ["retryAfter"] = "30",
                ["wwwAuthenticate"] = "Bearer",
            });

        Create(error, out var context);

        context.Response.Headers.RetryAfter.ToString().Should().Be("30");
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
    }

    [Fact]
    public void Create_ShouldNotEmitOrigin()
    {
        var problem = Create(AppErrors.Conflict("dup"), out _);

        problem.Extensions.Should().NotContainKey("origin");
        problem.Extensions.Should().NotContainKey("Origin");
    }

    [Fact]
    public void Create_ShouldOmitErrors_WhenNonValidationError()
    {
        var problem = Create(AppErrors.Conflict("dup"), out _);

        problem.Extensions.Should().NotContainKey("errors");
    }
}
