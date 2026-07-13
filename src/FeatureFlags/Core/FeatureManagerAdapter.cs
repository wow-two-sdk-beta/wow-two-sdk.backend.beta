using Microsoft.FeatureManagement;

namespace WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;

/// <summary>Default <see cref="IFeatureFlags"/> — delegates to <c>Microsoft.FeatureManagement</c>'s <see cref="IFeatureManager"/>.</summary>
public sealed class FeatureManagerAdapter : IFeatureFlags
{
    private readonly IFeatureManager _featureManager;

    /// <summary>Creates the adapter over the feature manager.</summary>
    /// <param name="featureManager">The Microsoft.FeatureManagement manager.</param>
    public FeatureManagerAdapter(IFeatureManager featureManager)
    {
        ArgumentNullException.ThrowIfNull(featureManager);
        _featureManager = featureManager;
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _featureManager.IsEnabledAsync(feature);
    }
}
