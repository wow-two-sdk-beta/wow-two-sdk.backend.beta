namespace WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr;

/// <summary>Refers to outcome of probing whether an image tag exists in the registry — separates a missing image from one the caller isn't authorized to see.</summary>
public enum ImageCheck
{
    /// <summary>The image tag exists → the manifest request returned 200.</summary>
    Exists,

    /// <summary>No such image tag → the manifest request returned 404.</summary>
    Missing,

    /// <summary>The registry refused — neither anonymous nor the provided token could read it → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected registry status).</summary>
    Failed
}
