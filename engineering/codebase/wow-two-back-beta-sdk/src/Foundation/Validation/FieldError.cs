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

    /// <summary>
    /// Gets the rule's own operands — <c>MaxLength</c>, <c>ComparisonValue</c>, <c>From</c>/<c>To</c> — or <see langword="null"/> when the rule reported none.
    /// </summary>
    /// <remarks>
    /// Carried so a consumer can re-render the failure in its own voice or its own language from <see cref="Code"/> plus these operands,
    /// instead of displaying <see cref="Message"/> verbatim. Without them a client-side message catalogue can only emit parameterless text,
    /// which is a catalogue that cannot replace the message it exists to replace. Operands only — never the rejected value, which may be a secret.
    /// </remarks>
    public IReadOnlyDictionary<string, object>? Params { get; init; }

    /// <summary>Gets how much weight the failure carries. Defaults to <see cref="ValidationSeverity.Error"/>.</summary>
    /// <remarks>Only <see cref="ValidationSeverity.Error"/> reaches a <see cref="ValidationError"/>; advisory failures surface via <see cref="IValidator{T}.Inspect"/>.</remarks>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;
}
