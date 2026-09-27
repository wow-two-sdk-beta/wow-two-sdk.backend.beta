namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;

/// <summary>Defines the advisory validation findings raised while one operation runs.</summary>
/// <remarks>Advisories are warnings and suggestions: they travel with a successful result and never block it.</remarks>
public interface IValidationAdvisoryTracker
{
    /// <summary>Gets the recorded advisories in recording order, without duplicates.</summary>
    IReadOnlyList<FieldError> Advisories { get; }

    /// <summary>Records every advisory in <paramref name="failures"/>, ignoring blocking errors.</summary>
    /// <param name="failures">Failures at any severity, such as the output of <see cref="IValidator{T}.Inspect"/>.</param>
    void Record(IEnumerable<FieldError> failures);
}
