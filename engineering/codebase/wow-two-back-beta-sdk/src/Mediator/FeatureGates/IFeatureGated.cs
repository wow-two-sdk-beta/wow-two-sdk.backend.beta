namespace WoW.Two.Sdk.Backend.Beta.Mediator.FeatureGates;

/// <summary>Defines a request that runs only while every named feature flag is enabled.</summary>
public interface IFeatureGated
{
    /// <summary>The feature flags that must all be enabled.</summary>
    IReadOnlyList<string> RequiredFeatures { get; }
}
