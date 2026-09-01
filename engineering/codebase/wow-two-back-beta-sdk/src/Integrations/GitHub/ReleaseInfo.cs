namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>A single published GitHub release.</summary>
/// <param name="Tag">The release tag (e.g. <c>v1.2.0</c>).</param>
/// <param name="PublishedAtUtc">When the release was published, when reported.</param>
public sealed record ReleaseInfo(string Tag, DateTimeOffset? PublishedAtUtc);
