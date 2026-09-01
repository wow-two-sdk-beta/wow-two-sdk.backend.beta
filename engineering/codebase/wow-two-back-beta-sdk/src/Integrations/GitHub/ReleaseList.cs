namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>The outcome of a releases lookup — the categorized result plus any resolved releases.</summary>
/// <param name="Outcome">How the lookup resolved.</param>
/// <param name="Releases">The releases, newest first; empty unless <paramref name="Outcome"/> is <see cref="ReleaseLookup.Found"/>.</param>
public sealed record ReleaseList(ReleaseLookup Outcome, IReadOnlyList<ReleaseInfo> Releases)
{
    /// <summary>An empty list result for a non-<see cref="ReleaseLookup.Found"/> outcome.</summary>
    /// <param name="outcome">The categorized lookup outcome.</param>
    /// <returns>A <see cref="ReleaseList"/> carrying no releases.</returns>
    public static ReleaseList Empty(ReleaseLookup outcome)
    {
        return new ReleaseList(outcome, []);
    }
}
