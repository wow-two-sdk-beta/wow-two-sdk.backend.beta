namespace WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr;

/// <summary>Reads image metadata from a container registry to confirm a published, deployable image exists.</summary>
public interface IContainerRegistryClient
{
    /// <summary>Probes whether the image for <paramref name="repo"/> at <paramref name="tag"/> (i.e. <c>ghcr.io/{owner}/{repo}:{tag}</c>) is published.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference the image is named after.</param>
    /// <param name="tag">The image tag to probe.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An <see cref="ImageCheck"/> categorizing the outcome.</returns>
    Task<ImageCheck> ImageExistsAsync(string repo, string tag, CancellationToken ct);
}
