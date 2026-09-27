using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts;
using WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ErrorMapping;

/// <summary>Tracked advisories reach the success envelope with request-resolved messages.</summary>
public sealed class ValidationAdvisoryMapperTests
{
    private sealed class PrefixingMessageMapper : IFieldErrorMessageMapper
    {
        public string Map(FieldError error, HttpContext context) => $"localized:{error.Code}";
    }

    [Fact]
    public async Task Map_ResolvesTrackedAdvisoriesThroughTheFieldMessageSeam()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFieldErrorMessageMapper, PrefixingMessageMapper>();
        services.AddErrorHttpStatusMapping();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IValidationAdvisoryTracker>().Record(
        [
            new FieldError { Property = "Url", Code = "RedirectTargetInsecure", Message = "raw", Severity = ValidationSeverity.Warning },
        ]);

        var advisories = scope.ServiceProvider.GetRequiredService<IValidationAdvisoryMapper>()
            .Map(new DefaultHttpContext { RequestServices = scope.ServiceProvider });

        advisories.Should().ContainSingle().Which.Message.Should().Be("localized:RedirectTargetInsecure");
    }

    [Fact]
    public void Ok_SerializesWarningsBesideData_AndOmitsThemWhenNone()
    {
        var warning = new FieldError { Property = "Url", Code = "RedirectTargetPort", Message = "port", Severity = ValidationSeverity.Info };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        using var withWarnings = JsonDocument.Parse(JsonSerializer.Serialize(ApiResponse<string>.Ok("payload", [warning]), options));
        using var withoutWarnings = JsonDocument.Parse(JsonSerializer.Serialize(ApiResponse<string>.Ok("payload", []), options));

        withWarnings.RootElement.GetProperty("warnings")[0].GetProperty("code").GetString().Should().Be("RedirectTargetPort");
        withoutWarnings.RootElement.TryGetProperty("warnings", out _).Should().BeFalse();
        withoutWarnings.RootElement.GetProperty("data").GetString().Should().Be("payload");
    }
}
