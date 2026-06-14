namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents a single field-level validation error.</summary>
/// <param name="Property">Name or path of the offending member; empty for object-level rules.</param>
/// <param name="Message">Human-readable description of what failed.</param>
/// <param name="Code">Stable, machine-readable code identifying the violated rule.</param>
public sealed record ValidationError(string Property, string Message, string Code);
