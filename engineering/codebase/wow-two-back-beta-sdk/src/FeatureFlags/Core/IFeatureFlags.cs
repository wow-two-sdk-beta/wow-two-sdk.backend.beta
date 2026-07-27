namespace WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;

/// <summary>
/// A minimal, vendor-neutral feature-flag check. The default implementation wraps
/// <c>Microsoft.FeatureManagement</c>; swap it for an OpenFeature-backed one without touching call sites.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>Returns whether the named feature is currently enabled.</summary>
    /// <param name="feature">The feature name.</param>
    /// <param name="cancellationToken">Token to cancel the evaluation.</param>
    /// <returns><see langword="true"/> when the feature is on.</returns>
    ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default);
}
