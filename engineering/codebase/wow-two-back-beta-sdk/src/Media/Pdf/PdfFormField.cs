namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents one interactive form field: its name, kind, value and the choices it offers.</summary>
public sealed record PdfFormField
{
    /// <summary>Gets the field name, as a fill request addresses it.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the kind.</summary>
    public required PdfFormFieldType Type { get; init; }

    /// <summary>Gets the current value; empty when unset.</summary>
    public required string Value { get; init; }

    /// <summary>Gets the choices of a list, combo box or radio group, or the on-state of a check box.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Gets whether viewers refuse edits.</summary>
    public required bool ReadOnly { get; init; }
}
