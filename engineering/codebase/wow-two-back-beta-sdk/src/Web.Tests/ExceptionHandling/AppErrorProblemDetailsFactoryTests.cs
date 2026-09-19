using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ExceptionHandling;

public sealed class AppErrorProblemDetailsFactoryTests
{
    private static readonly IErrorHttpStatusCodeMapper Mapper = new ErrorHttpStatusCodeMapper();
    private static readonly IErrorMessageMapper Resolver = new ErrorMessageMapper();
    private static readonly IFieldErrorMessageMapper FieldResolver = new FieldErrorMessageMapper();

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, out HttpContext context)
    {
        context = new DefaultHttpContext();
        return CreateFactory().Create(error, context);
    }

    private static AppErrorProblemDetailsFactory CreateFactory(
        IFieldErrorMessageMapper? fieldMessageMapper = null) =>
        new(Mapper, Resolver, fieldMessageMapper ?? FieldResolver);

    [Fact]
    public void Create_ShouldSetStatusTypeCodeAndDetail()
    {
        var problem = Create(AppErrorFactory.NotFound("Order 3f2 not found."), out var context);

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
        var problem = Create(AppErrorFactory.Conflict("dup"), out _);

        problem.Extensions.Should().NotContainKey("origin");
        problem.Extensions.Should().NotContainKey("Origin");
    }

    [Fact]
    public void Create_ShouldOmitErrors_WhenNonValidationError()
    {
        var problem = Create(AppErrorFactory.Conflict("dup"), out _);

        problem.Extensions.Should().NotContainKey("errors");
    }

    [Fact]
    public void Create_ShouldEmitParams_WhenFailureCarriesOperands()
    {
        var error = ValidationError.From(
        [
            new FieldError
            {
                Property = "name",
                Message = "too long",
                Code = "MaximumLengthValidator",
                Params = new Dictionary<string, object> { ["MaxLength"] = 3 },
            },
        ]);

        var problem = Create(error, out _);

        problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<FieldError>>()
            .Which.Single().Params!["MaxLength"].Should().Be(3);
    }

    [Fact]
    public void Create_ShouldRewriteFieldMessages_WhenFieldResolverSupplied()
    {
        // The plug for localization: without it, `detail` is resolved and every `errors[]` message is not.
        var error = ValidationError.From(
        [
            new FieldError { Property = "email", Message = "Email is required.", Code = "NotEmptyValidator" },
        ]);
        var context = new DefaultHttpContext();

        var problem = CreateFactory(new UpperCaseFieldMessageResolver()).Create(error, context);

        problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<FieldError>>()
            .Which.Single().Message.Should().Be("EMAIL IS REQUIRED.");
    }

    [Fact]
    public void Create_ShouldPreserveFieldMessages_WhenDefaultFieldResolverSupplied()
    {
        var error = ValidationError.From(
        [
            new FieldError { Property = "email", Message = "Email is required.", Code = "NotEmptyValidator" },
        ]);
        var context = new DefaultHttpContext();

        var problem = CreateFactory().Create(error, context);

        problem.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<FieldError>>()
            .Which.Single().Message.Should().Be("Email is required.");
    }

    [Fact]
    public void Registration_ShouldPreserveFactoryRegisteredFirst()
    {
        var services = new ServiceCollection();
        var replacement = new ReplacementProblemDetailsFactory();
        services.AddSingleton<IAppErrorProblemDetailsFactory>(replacement);

        services.AddAppExceptionHandling();

        services.BuildServiceProvider().GetRequiredService<IAppErrorProblemDetailsFactory>()
            .Should().BeSameAs(replacement);
    }

    /// <summary>Stands in for a localizing resolver — proves the seam reaches every failure's message.</summary>
    private sealed class UpperCaseFieldMessageResolver : IFieldErrorMessageMapper
    {
        public string Map(FieldError error, HttpContext context) => error.Message.ToUpperInvariant();
    }

    /// <summary>Creates a fixed ProblemDetails for registration replacement coverage.</summary>
    private sealed class ReplacementProblemDetailsFactory : IAppErrorProblemDetailsFactory
    {
        public Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, HttpContext httpContext) => new();
    }
}
