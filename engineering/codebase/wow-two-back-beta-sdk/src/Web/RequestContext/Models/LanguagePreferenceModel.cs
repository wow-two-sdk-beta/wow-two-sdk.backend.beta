namespace WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;

/// <summary>Represents a basic language range and its HTTP preference weight.</summary>
public sealed record LanguagePreferenceModel
{
    /// <summary>Gets the normalized lowercase range, including wildcard.</summary>
    public required string Range { get; init; }

    /// <summary>Gets the quality from zero (excluded) to one (preferred).</summary>
    public required double Quality { get; init; }
}
