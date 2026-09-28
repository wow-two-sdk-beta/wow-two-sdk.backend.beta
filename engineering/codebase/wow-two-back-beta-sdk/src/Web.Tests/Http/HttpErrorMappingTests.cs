using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Http.Errors;
using WoW.Two.Sdk.Backend.Beta.Http.Safety;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Http;

/// <summary>Outbound HTTP failures become application errors instead of anonymous 500s.</summary>
public sealed class HttpErrorMappingTests
{
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, AppErrorType.ExternalUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized, AppErrorType.ExternalUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, AppErrorType.ExternalUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout, AppErrorType.OperationTimeout)]
    public void Rule_ClassifiesDependencyStatuses(HttpStatusCode status, AppErrorType expected)
        => new HttpExceptionMappingRule().TryMap(new HttpRequestException("failed", null, status))!.Type.Should().Be(expected);

    [Fact]
    public void Rule_ClassifiesTransportFailuresAndLeavesOthersAlone()
    {
        var rule = new HttpExceptionMappingRule();

        rule.TryMap(new HttpRequestException("connection refused"))!.Type.Should().Be(AppErrorType.ExternalUnavailable);
        rule.TryMap(new TaskCanceledException("timed out", new TimeoutException()))!.Type.Should().Be(AppErrorType.OperationTimeout);
        rule.TryMap(new OutboundAddressBlockedException("10.0.0.1"))!.Type.Should().Be(AppErrorType.Forbidden);
        rule.TryMap(new HttpRequestException("gone", null, HttpStatusCode.NotFound)).Should().BeNull();
        rule.TryMap(new InvalidOperationException()).Should().BeNull();
    }

    [Fact]
    public async Task ToAppError_KeepsTheUpstreamStatusDetailAndRetryAfter()
    {
        using var throttled = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("""{"title":"Busy","detail":"Queue full"}""", Encoding.UTF8, "application/problem+json"),
        };
        throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

        var error = await throttled.ToAppErrorAsync();

        error!.Type.Should().Be(AppErrorType.ExternalUnavailable);
        error.Metadata.Should().Contain(new KeyValuePair<string, object?>("upstreamStatus", 503))
            .And.Contain(new KeyValuePair<string, object?>("upstreamDetail", "Queue full"))
            .And.Contain(new KeyValuePair<string, object?>("retryAfter", "30"));

        using var missing = new HttpResponseMessage(HttpStatusCode.NotFound);
        (await missing.ToAppErrorAsync())!.Type.Should().Be(AppErrorType.NotFound);
        using var ok = new HttpResponseMessage(HttpStatusCode.OK);
        (await ok.ToAppErrorAsync()).Should().BeNull();
        await ok.Invoking(response => response.EnsureSuccessAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApiDefaultsHost_AnswersADependencyOutageWith503()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        await using var app = builder.Build();
        app.UseApiDefaults();
        app.MapGet("/quote", () => { throw new HttpRequestException("upstream down", null, HttpStatusCode.BadGateway); });
        await app.StartAsync();

        using var response = await app.GetTestServer().CreateClient().GetAsync("/quote");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be("ExternalUnavailable");
    }
}
