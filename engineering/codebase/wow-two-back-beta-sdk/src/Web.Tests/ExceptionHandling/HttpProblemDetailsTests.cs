using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ExceptionHandling;

public sealed class HttpProblemDetailsTests
{
    private static async Task<WebApplication> Host(bool filterHosts = false,
        Action<ProblemDetailsContext>? customize = null, string environment = "Production")
    {
        var options = new WebApplicationOptions { EnvironmentName = environment };
        var builder = filterHosts ? WebApplication.CreateBuilder(options) : WebApplication.CreateSlimBuilder(options);
        if (filterHosts) builder.Configuration["AllowedHosts"] = "allowed.test";
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAppExceptionHandling();
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddHttpProblemDetails();
        if (customize is not null)
            builder.Services.Configure<ProblemDetailsOptions>(problemOptions => problemOptions.CustomizeProblemDetails = customize);
        builder.Services.Configure<MvcOptions>(options => options.ReturnHttpNotAcceptable = true);
        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemContractController).Assembly);
        var app = builder.Build();
        app.UseHttpProblemDetails();
        app.MapControllers();
        app.MapGet("/empty/{status:int}", (HttpContext context, int status) =>
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            context.Response.Headers.RetryAfter = "12";
            context.Response.StatusCode = status;
        });
        app.MapGet("/app-error", void () => AppErrorFactory.NotFound("The requested item was not found.").Throw());
        app.MapMethods("/unexpected", ["GET", "HEAD"], void () => throw new InvalidOperationException("private-secret"));
        app.MapGet("/get-only", () => Results.Ok());
        app.MapGet("/bad-request", void () => throw new BadHttpRequestException("private-wire-detail", 413));
        app.MapGet("/minimal-validation", () => Results.ValidationProblem(
            new Dictionary<string, string[]> { ["name"] = ["Name is required"] }));
        app.MapGet("/problem-context", async (HttpContext context, IProblemDetailsService problems) =>
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400 },
                Exception = new InvalidOperationException("private-context-error"),
                AdditionalMetadata = new EndpointMetadataCollection("review-metadata"),
            }));
        app.MapGet("/started", async (HttpContext context) =>
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("owned stream");
        });
        app.MapGet("/success", () => Results.Ok(new { value = 42 }));
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task HostFilteringBeforeTheApplicationHasAProblemBody()
    {
        await using var app = await Host(filterHosts: true);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/success");
        request.Headers.Host = "rejected.test";
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.RootElement.GetProperty("traceId").GetString()!);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("/empty/401", 401)]
    [InlineData("/empty/403", 403)]
    [InlineData("/empty/404", 404)]
    [InlineData("/empty/405", 405)]
    [InlineData("/empty/415", 415)]
    [InlineData("/empty/429", 429)]
    [InlineData("/empty/503", 503)]
    [InlineData("/unknown", 404)]
    [InlineData("/app-error", 404)]
    [InlineData("/unexpected", 500)]
    [InlineData("/bad-request", 413)]
    [InlineData("/contract/bare", 400)]
    [InlineData("/contract/string", 400)]
    [InlineData("/contract/object", 409)]
    [InlineData("/contract/json", 400)]
    [InlineData("/contract/implicit-status", 409)]
    [InlineData("/contract/validation", 400)]
    [InlineData("/contract/response-status-json", 400)]
    [InlineData("/contract/response-status-object", 400)]
    [InlineData("/contract/response-status-content", 400)]
    [InlineData("/minimal-validation", 400)]
    public async Task AllFailuresHaveSafeProblemBodiesDespiteHtmlAccept(string path, int status)
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        foreach (var accept in new[] { "text/html", "application/json" })
        {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd(accept);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.RootElement.GetProperty("type").GetString()!);
        Assert.NotEmpty(body.RootElement.GetProperty("title").GetString()!);
        Assert.Equal(path, body.RootElement.GetProperty("instance").GetString());
        Assert.NotEmpty(body.RootElement.GetProperty("requestId").GetString()!);
        Assert.NotEmpty(body.RootElement.GetProperty("traceId").GetString()!);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("private-", text);
        if (path is "/contract/validation" or "/minimal-validation")
            Assert.Equal("Name is required", body.RootElement.GetProperty("errors").GetProperty("name")[0].GetString());
        if (path.StartsWith("/empty/", StringComparison.Ordinal))
        {
            Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
            Assert.Equal(TimeSpan.FromSeconds(12), response.Headers.RetryAfter?.Delta);
        }
        }
    }

    [Theory]
    [InlineData("/contract/bare")]
    [InlineData("/contract/string")]
    [InlineData("/contract/implicit-status")]
    [InlineData("/contract/validation")]
    [InlineData("/minimal-validation")]
    [InlineData("/empty/400")]
    public async Task CustomizationRunsOnceForEachProblem(string path)
    {
        var calls = 0;
        await using var app = await Host(customize: context =>
        {
            calls++;
            context.ProblemDetails.Extensions.Add("custom", "value");
        });
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(path == "/contract/implicit-status" ? 409 : 400, (int)response.StatusCode);
        Assert.Equal(1, calls);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("value", body.RootElement.GetProperty("custom").GetString());
    }

    [Fact]
    public async Task CustomizationRetainsTheOriginalProblemContext()
    {
        ProblemDetailsContext? observed = null;
        await using var app = await Host(customize: context => observed = context);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/problem-context");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(observed);
        Assert.IsType<InvalidOperationException>(observed.Exception);
        Assert.Equal("review-metadata", observed.AdditionalMetadata?.OfType<string>().Single());
        Assert.DoesNotContain("private-context-error", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Production", "GET")]
    [InlineData("Development", "GET")]
    [InlineData("Production", "HEAD")]
    public async Task ExceptionCustomizationRetainsTheExceptionWithoutExposingDetails(string environment, string method)
    {
        Exception? observed = null;
        await using var app = await Host(customize: context => observed = context.Exception, environment: environment);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/unexpected");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.IsType<InvalidOperationException>(observed);
        Assert.DoesNotContain("private-secret", await response.Content.ReadAsStringAsync());
        if (method == "HEAD") Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        else Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RoutingAndModelBindingFailuresRemainProblemDetails()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        foreach (var item in new[] { ("/get-only", "application/json", "{}", 405),
            ("/contract/input", "application/json", "{broken", 400),
            ("/contract/input", "text/plain", "text", 415) })
        {
            using var content = new StringContent(item.Item3, System.Text.Encoding.UTF8, item.Item2);
            using var response = await client.PostAsync(item.Item1, content);
            Assert.Equal(item.Item4, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(item.Item4, body.RootElement.GetProperty("status").GetInt32());
            Assert.NotEmpty(body.RootElement.GetProperty("traceId").GetString()!);
        }
    }

    [Fact]
    public async Task HeadErrorsHaveNoBodyAndSuccessRemainsUnchanged()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/unexpected"));
        Assert.Equal(HttpStatusCode.InternalServerError, head.StatusCode);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        using var success = await client.GetAsync("/success");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Contains("42", await success.Content.ReadAsStringAsync());
        using var stream = await client.GetAsync("/started");
        Assert.Equal("owned stream", await stream.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RedirectCanReplaceAPreviouslyAssignedErrorStatus()
    {
        await using var app = await Host();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/contract/redirect");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/success", response.Headers.Location?.OriginalString);
    }
}

[ApiController]
[Route("contract")]
public sealed class ProblemContractController : ControllerBase
{
    [HttpGet("redirect")]
    public IActionResult RedirectAfterErrorStatus()
    {
        Response.StatusCode = 400;
        return Redirect("/success");
    }

    [HttpGet("response-status-json")]
    public IActionResult ResponseStatusJson()
    {
        Response.StatusCode = 400;
        return new JsonResult(new { error = "private-secret" });
    }

    [HttpGet("response-status-object")]
    public IActionResult ResponseStatusObject()
    {
        Response.StatusCode = 400;
        return new ObjectResult(new { error = "private-secret" });
    }

    [HttpGet("response-status-content")]
    public IActionResult ResponseStatusContent()
    {
        Response.StatusCode = 400;
        return Content("private-secret");
    }

    [HttpGet("bare")]
    public IActionResult Bare() { return BadRequest(); }

    [HttpGet("string")]
    public IActionResult String() { return BadRequest("private-detail"); }

    [HttpGet("object")]
    public IActionResult Object() { return Conflict(new { error = "private-secret" }); }

    [HttpGet("json")]
    public IActionResult JsonError() { return new JsonResult(new { error = "private-secret" }) { StatusCode = 400 }; }

    [HttpGet("implicit-status")]
    public IActionResult ImplicitStatus() { return new ObjectResult(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 409 }); }

    [HttpPost("input")]
    public IActionResult Input([FromBody] ProblemInput input) { return Ok(input); }

    public sealed record ProblemInput(string Name);

    [HttpGet("validation")]
    public IActionResult Invalid()
    {
        return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["Name is required"] }));
    }
}
