using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;
/// <summary>An external login (Google/Microsoft/Telegram/…) linked to a user (composite key provider + provider-key).</summary>
/// <typeparam name="TKey">User key type.</typeparam>
public class IdentityUserLogin<TKey> : ICompositeKeyEntity
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>External provider name (e.g. <c>Google</c>).</summary>
    public string LoginProvider { get; set; } = string.Empty;

    /// <summary>The user's id at the provider.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>Friendly provider name for display.</summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>The local user this login maps to.</summary>
    public TKey UserId { get; set; } = default!;
}
