using AwesomeAssertions;
using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers.Extensions;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Validation;

/// <summary>Handler-side validation blocks on errors and tracks the advisories of a passing instance.</summary>
public sealed class ValidatorAdvisoryExtensionsTests
{
    private sealed record Link(string Name, string Url);

    private sealed class LinkValidator : AbstractValidator<Link>
    {
        public LinkValidator()
        {
            RuleFor(link => link.Name).NotEmpty();
            RuleFor(link => link.Url).Must(url => url.StartsWith("https:", StringComparison.Ordinal))
                .WithSeverity(Severity.Warning).WithErrorCode("Insecure");
        }
    }

    private readonly FluentValidationAdapter<Link> _validator = new([new LinkValidator()]);

    [Fact]
    public void Validate_RecordsAdvisories_WhenTheInstancePasses()
    {
        var tracker = new ValidationAdvisoryTracker();

        var error = _validator.Validate(new Link("Menu", "http://menu.example"), tracker);

        error.Should().BeNull();
        tracker.Advisories.Should().ContainSingle()
            .Which.Should().Match<FieldError>(advisory => advisory.Code == "Insecure" && advisory.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_ReturnsErrorsAndRecordsNothing_WhenTheInstanceIsBlocked()
    {
        var tracker = new ValidationAdvisoryTracker();

        var error = _validator.Validate(new Link("", "http://menu.example"), tracker);

        error.Should().NotBeNull();
        error!.Failures.Should().ContainSingle().Which.Property.Should().Be(nameof(Link.Name));
        tracker.Advisories.Should().BeEmpty();
    }
}
