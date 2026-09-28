namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Represents one of a user's passkeys, without its key material.</summary>
public sealed record PasskeyModel
{
    /// <summary>Gets the passkey id, for removal.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user's label.</summary>
    public required string Name { get; init; }

    /// <summary>Gets when it was registered.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when it last signed in.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
