using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Extensions;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling.Factories;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ExceptionHandling;

public sealed class ControllerProblemExtensionsTests
{
    [Fact]
    public void ToProblem_UsesRegisteredFactoryAndPreservesItsStatusAndHeaders()
    {
        var services = new ServiceCollection().AddSingleton<IAppErrorProblemDetailsFactory, CustomFactory>();
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var controller = new TestController { ControllerContext = new ControllerContext { HttpContext = http } };
        var result = controller.ToProblem(AppErrorFactory.NotFound("missing"));
        Assert.Equal(429, result.StatusCode);
        Assert.Equal("custom:missing", Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(result.Value).Detail);
        Assert.Equal("30", http.Response.Headers.RetryAfter.ToString());
    }

    private sealed class TestController : ControllerBase;
    private sealed class CustomFactory : IAppErrorProblemDetailsFactory
    {
        public Microsoft.AspNetCore.Mvc.ProblemDetails Create(AppError error, HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = "30";
            return new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 429, Detail = "custom:" + error.Message };
        }
    }
}
