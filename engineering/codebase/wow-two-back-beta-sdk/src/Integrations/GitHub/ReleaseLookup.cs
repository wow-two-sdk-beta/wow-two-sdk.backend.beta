namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Refers to outcome of a releases lookup — separates a repo with no releases from one the token can't see or a transport failure.</summary>
public enum ReleaseLookup
{
    /// <summary>One or more releases were resolved → <see cref="ReleaseList.Releases"/> is populated.</summary>
    Found,

    /// <summary>The repo exists but has published no releases yet → 404 on the releases endpoint.</summary>
    None,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The lookup could not be completed (transport error or an unexpected status).</summary>
    Failed
}
