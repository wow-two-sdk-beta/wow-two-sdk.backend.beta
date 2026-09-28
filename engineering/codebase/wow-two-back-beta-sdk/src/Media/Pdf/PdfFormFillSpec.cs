namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents values for a form's fields and what happens around filling them.</summary>
public sealed record PdfFormFillSpec
{
    /// <summary>
    /// Gets the values by field name: text as is; check boxes take <c>true</c>, <c>yes</c>, <c>on</c>, <c>1</c> or their
    /// on-state name, anything else unchecks; lists, combo boxes and radio groups take one of their options.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Values { get; init; }

    /// <summary>Gets whether every field turns read-only after filling, so the result reads as final. Default false.</summary>
    public bool LockFields { get; init; }

    /// <summary>Gets whether names the form lacks are skipped rather than refused. Default false.</summary>
    public bool IgnoreUnknownFields { get; init; }
}
