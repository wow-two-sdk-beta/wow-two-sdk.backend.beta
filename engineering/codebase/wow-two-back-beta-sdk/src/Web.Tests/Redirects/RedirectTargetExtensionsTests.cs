using AwesomeAssertions;
using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.Redirects.Extensions;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Redirects;

/// <summary>Redirect target checks advise with stable codes and severities, and never block a value.</summary>
public sealed class RedirectTargetExtensionsTests
{
    private sealed record Target
    {
        public required string Url { get; init; }
    }

    private sealed class TargetValidator : AbstractValidator<Target>
    {
        public TargetValidator() => RuleFor(target => target.Url).AdviseOnRedirectTarget();
    }

    private static readonly FluentValidationAdapter<Target> Validator = new([new TargetValidator()]);

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("https://sub.example.co.uk:443/")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void Inspect_ReportsNothing_ForPublicHttpsOrStructurallyInvalidValues(string url) =>
        Validator.Inspect(new Target { Url = url }).Should().BeEmpty();

    [Theory]
    [InlineData("https://192.168.1.10/", "RedirectTargetPrivateNetwork")]
    [InlineData("https://10.0.0.5/admin", "RedirectTargetPrivateNetwork")]
    [InlineData("https://intranet/wiki", "RedirectTargetPrivateNetwork")]
    [InlineData("https://printer.local/", "RedirectTargetPrivateNetwork")]
    [InlineData("https://localhost/", "RedirectTargetPrivateNetwork")]
    [InlineData("https://app.test/", "RedirectTargetReservedName")]
    [InlineData("http://example.com/", "RedirectTargetInsecure")]
    [InlineData("https://user:secret@example.com/", "RedirectTargetCredentials")]
    public void Inspect_ReportsAWarning_ForTargetsThatMayNotWorkEverywhere(string url, string code)
    {
        var findings = Validator.Inspect(new Target { Url = url });

        findings.Should().ContainSingle(finding => finding.Code == code)
            .Which.Should().Match<FieldError>(finding =>
                finding.Severity == ValidationSeverity.Warning && finding.Property == nameof(Target.Url));
        Validator.Validate(new Target { Url = url }).Should().BeNull("advisories never block the value");
    }

    [Theory]
    [InlineData("https://8.8.8.8/", "RedirectTargetIpAddress")]
    [InlineData("https://example.com:8443/", "RedirectTargetPort")]
    [InlineData("https://bücher.de/", "RedirectTargetInternationalHost")]
    [InlineData("https://xn--bcher-kva.de/", "RedirectTargetInternationalHost")]
    public void Inspect_ReportsASuggestion_ForTargetsWorthConfirming(string url, string code) =>
        Validator.Inspect(new Target { Url = url }).Should().ContainSingle()
            .Which.Should().Match<FieldError>(finding => finding.Code == code && finding.Severity == ValidationSeverity.Info);

    [Fact]
    public void Inspect_ReportsEveryApplicableFinding_WhenSeveralApply()
    {
        var codes = Validator.Inspect(new Target { Url = "http://[::1]:8080/" }).Select(finding => finding.Code);

        codes.Should().BeEquivalentTo(["RedirectTargetPrivateNetwork", "RedirectTargetInsecure", "RedirectTargetPort"]);
    }
}
