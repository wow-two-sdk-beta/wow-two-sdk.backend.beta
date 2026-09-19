namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>A single published GitHub release.</summary>
public sealed record ReleaseInfo
{
    /// <summary>The release tag (e.g. <c>v1.2.0</c>).</summary>
    public required string Tag { get; init; }

    /// <summary>When the release was published, when reported.</summary>
    public required DateTimeOffset? PublishedAtUtc { get; init; }
}
