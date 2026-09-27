namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation.Trackers;

/// <summary>Tracks the advisory validation findings raised within one dependency-injection scope.</summary>
/// <remarks>Register scoped: one HTTP request or one background unit owns one tracker.</remarks>
public sealed class ValidationAdvisoryTracker : IValidationAdvisoryTracker
{
    private readonly Lock _gate = new();
    private readonly List<FieldError> _advisories = [];

    /// <inheritdoc />
    public IReadOnlyList<FieldError> Advisories
    {
        get
        {
            lock (_gate)
            {
                return [.. _advisories];
            }
        }
    }

    /// <inheritdoc />
    public void Record(IEnumerable<FieldError> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        lock (_gate)
        {
            foreach (FieldError failure in failures)
            {
                if (failure.Severity != ValidationSeverity.Error && !_advisories.Any(existing => IsSame(existing, failure)))
                {
                    _advisories.Add(failure);
                }
            }
        }
    }

    private static bool IsSame(FieldError left, FieldError right) =>
        left.Severity == right.Severity
        && string.Equals(left.Property, right.Property, StringComparison.Ordinal)
        && string.Equals(left.Code, right.Code, StringComparison.Ordinal);
}
