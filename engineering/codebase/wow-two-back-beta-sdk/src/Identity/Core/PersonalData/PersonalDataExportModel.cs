namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;

/// <summary>Represents a user's personal-data export: when it was made and one entry per section.</summary>
public sealed record PersonalDataExportModel
{
    /// <summary>Gets when the export was made.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Gets the sections: <c>account</c>, <c>claims</c>, <c>logins</c>, <c>passkeys</c>, <c>sessions</c>, then the product's.</summary>
    public required IReadOnlyDictionary<string, object?> Sections { get; init; }
}
