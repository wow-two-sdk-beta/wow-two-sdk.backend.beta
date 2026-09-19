namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>The outcome of a releases lookup — the categorized result plus any resolved releases.</summary>
public sealed record ReleaseList
{
    /// <summary>How the lookup resolved.</summary>
    public required ReleaseLookup Outcome { get; init; }

    /// <summary>The releases, newest first; empty unless <see cref="Outcome"/> is <see cref="ReleaseLookup.Found"/>.</summary>
    public required IReadOnlyList<ReleaseInfo> Releases { get; init; }

    /// <summary>An empty list result for a non-<see cref="ReleaseLookup.Found"/> outcome.</summary>
    /// <param name="outcome">The categorized lookup outcome.</param>
    /// <returns>A <see cref="ReleaseList"/> carrying no releases.</returns>
    public static ReleaseList Empty(ReleaseLookup outcome)
    {
        return new ReleaseList { Outcome = outcome, Releases = [] };
    }
}
