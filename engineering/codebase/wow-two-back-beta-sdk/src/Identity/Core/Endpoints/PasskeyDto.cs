namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>One of the account's passkeys, without key material.</summary>
public sealed record PasskeyDto
{
    /// <summary>The passkey id, for removal.</summary>
    public required Guid Id { get; init; }

    /// <summary>The label given at registration.</summary>
    public required string Name { get; init; }

    /// <summary>When it was registered.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it last signed in; null before its first sign-in.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
}
