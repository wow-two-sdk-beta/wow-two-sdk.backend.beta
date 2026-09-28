namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>A single failure from an identity operation.</summary>
public readonly record struct IdentityError
{
    /// <summary>Stable error code (e.g. <c>DuplicateUserName</c>).</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Values the description was built from, by placeholder name — lets translation catalogs rebuild it.</summary>
    public IReadOnlyDictionary<string, object>? Params { get; init; }
}
