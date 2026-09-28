using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Meta;

/// <summary>An <c>AddApiDefaults</c> host translates thrown errors after nothing but a configuration change.</summary>
public sealed class ApiDefaultsErrorTranslationTests
{
    [Theory]
    [InlineData(false, "Order 42 was not found.")]
    [InlineData(true, "Запрошенный ресурс не найден.")]
    public async Task ThrownError_IsTranslatedOnlyWhenConfigurationEnablesIt(bool enabled, string expected)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ErrorTranslation:Enabled"] = enabled ? "true" : "false",
            ["ErrorTranslation:SupportedCultures:0"] = "ru",
        });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        await using var app = builder.Build();
        app.UseApiDefaults();
        app.MapGet("/orders/42", () => AppErrorFactory.NotFound("Order 42 was not found.").Throw());
        await app.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/orders/42");
        request.Headers.AcceptLanguage.ParseAdd("ru-RU");

        using var response = await app.GetTestServer().CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString().Should().Be(expected);
    }
}
