namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents a single field-level validation failure.</summary>
public sealed record FieldError
{
    /// <summary>Gets the member path that failed.</summary>
    public required string Property { get; init; }

    /// <summary>Gets the human-readable failure message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the stable rule code (from the validator).</summary>
    public required string Code { get; init; }
}
