using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Validation;

/// <summary>The advisory tracker keeps warnings and suggestions, never errors, once each and in order.</summary>
public sealed class ValidationAdvisoryTrackerTests
{
    [Fact]
    public void Record_KeepsAdvisoriesInOrderAndIgnoresErrors()
    {
        var tracker = new ValidationAdvisoryTracker();

        tracker.Record(
        [
            Failure("Url", "Blocking", ValidationSeverity.Error),
            Failure("Url", "Insecure", ValidationSeverity.Warning),
            Failure("Url", "Port", ValidationSeverity.Info),
        ]);

        tracker.Advisories.Select(advisory => advisory.Code).Should().Equal("Insecure", "Port");
    }

    [Fact]
    public void Record_DropsARepeatedFindingForTheSameMember()
    {
        var tracker = new ValidationAdvisoryTracker();

        tracker.Record([Failure("Url", "Insecure", ValidationSeverity.Warning)]);
        tracker.Record([Failure("Url", "Insecure", ValidationSeverity.Warning), Failure("Other", "Insecure", ValidationSeverity.Warning)]);

        tracker.Advisories.Select(advisory => advisory.Property).Should().Equal("Url", "Other");
    }

    private static FieldError Failure(string property, string code, ValidationSeverity severity) =>
        new() { Property = property, Code = code, Message = code, Severity = severity };
}
