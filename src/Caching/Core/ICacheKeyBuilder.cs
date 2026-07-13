namespace WoW.Two.Sdk.Backend.Beta.Caching.Core;

/// <summary>Builds consistent, collision-resistant cache keys from ordered parts using a single convention.</summary>
public interface ICacheKeyBuilder
{
    /// <summary>Joins the given parts into a single cache key (colon-delimited by convention).</summary>
    /// <param name="parts">The key segments, most-general first (e.g. <c>"user", id, "profile"</c>).</param>
    /// <returns>The composed cache key.</returns>
    string Build(params string[] parts);
}
