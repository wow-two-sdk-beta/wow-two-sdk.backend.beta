namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents the outcome of validating an object.</summary>
public abstract record ValidationResult
{
    private ValidationResult() { }

    /// <summary>Gets a value indicating whether validation passed.</summary>
    public bool IsValid => this is Success;

    /// <summary>Validation passed — every rule was satisfied.</summary>
    public sealed record Success : ValidationResult;

    /// <summary>Validation failed — carries the errors found.</summary>
    /// <param name="Errors">The validation errors found; never empty for a failure.</param>
    public sealed record Failure(IReadOnlyList<ValidationError> Errors) : ValidationResult;
}
