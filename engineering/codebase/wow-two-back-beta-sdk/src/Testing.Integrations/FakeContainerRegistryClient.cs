using WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Integrations;

/// <summary>
/// Configurable test double for <see cref="IContainerRegistryClient"/> — short-circuits the GHCR manifest probe
/// so a test never reaches the real registry.
/// </summary>
/// <remarks>
///   - probe order: <see cref="Override"/> wins, then <see cref="ExistingImages"/>, then <see cref="ExistingTags"/>
///   - a tag matching none of them reports <see cref="ImageCheck.Missing"/>
///   - use <see cref="ExistingTags"/> for a single-repo suite, <see cref="ExistingImages"/> across repos
/// </remarks>
public sealed class FakeContainerRegistryClient : IContainerRegistryClient
{
    /// <summary>The tags whose images report as published for any repo; any other tag reports <see cref="ImageCheck.Missing"/>.</summary>
    public HashSet<string> ExistingTags { get; } = new(StringComparer.Ordinal);

    /// <summary>The fully-qualified <c>{repo}:{tag}</c> images that report as published; checked before <see cref="ExistingTags"/>.</summary>
    public HashSet<string> ExistingImages { get; } = new(StringComparer.Ordinal);

    /// <summary>When set, every probe returns this outcome regardless of the sets (for the unauthorized / failed paths).</summary>
    public ImageCheck? Override { get; set; }

    /// <summary>Marks <paramref name="tag"/>'s image as published for every repo and returns this instance for chaining.</summary>
    /// <param name="tag">The image tag to report as <see cref="ImageCheck.Exists"/>.</param>
    /// <returns>This <see cref="FakeContainerRegistryClient"/>.</returns>
    public FakeContainerRegistryClient WithTag(string tag)
    {
        ExistingTags.Add(tag);
        return this;
    }

    /// <summary>Marks the <c>{repo}:{tag}</c> image as published and returns this instance for chaining.</summary>
    /// <param name="repo">The <c>{owner}/{repo}</c> reference the image is named after.</param>
    /// <param name="tag">The image tag to report as <see cref="ImageCheck.Exists"/> for that repo.</param>
    /// <returns>This <see cref="FakeContainerRegistryClient"/>.</returns>
    public FakeContainerRegistryClient WithImage(string repo, string tag)
    {
        ExistingImages.Add(Key(repo, tag));
        return this;
    }

    /// <summary>Forces every probe to return <paramref name="outcome"/> (e.g. <see cref="ImageCheck.Unauthorized"/>) and returns this instance for chaining.</summary>
    /// <param name="outcome">The outcome to force regardless of the configured sets.</param>
    /// <returns>This <see cref="FakeContainerRegistryClient"/>.</returns>
    public FakeContainerRegistryClient WithOverride(ImageCheck outcome)
    {
        Override = outcome;
        return this;
    }

    /// <inheritdoc />
    public Task<ImageCheck> ImageExistsAsync(string repo, string tag, CancellationToken ct)
    {
        if (Override is { } forced)
            return Task.FromResult(forced);

        var exists = ExistingImages.Contains(Key(repo, tag)) || ExistingTags.Contains(tag);
        return Task.FromResult(exists ? ImageCheck.Exists : ImageCheck.Missing);
    }

    private static string Key(string repo, string tag) => $"{repo}:{tag}";
}
