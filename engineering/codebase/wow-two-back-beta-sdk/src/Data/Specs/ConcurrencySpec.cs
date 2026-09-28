namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Represents the property that guards an entity against lost updates, and how it changes.</summary>
public sealed record ConcurrencySpec
{
    /// <summary>The token property.</summary>
    public required string Property { get; init; }

    /// <summary>How the token changes on each write.</summary>
    public required ConcurrencyTokenKind Kind { get; init; }
}
