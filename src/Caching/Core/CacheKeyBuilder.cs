namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>Default <see cref="ICacheKeyBuilder"/> — joins parts with a colon (<c>user:42:profile</c>). Stateless and thread-safe.</summary>
public sealed class CacheKeyBuilder : ICacheKeyBuilder
{
    /// <inheritdoc />
    public string Build(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return string.Join(':', parts);
    }
}
