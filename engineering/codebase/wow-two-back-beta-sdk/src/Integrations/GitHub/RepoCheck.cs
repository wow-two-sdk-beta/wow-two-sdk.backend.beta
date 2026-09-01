namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Outcome of a repository-existence probe — separates a genuinely missing repo from one the token can't see.</summary>
public enum RepoCheck
{
    /// <summary>The repo exists and is visible to the current token → 200.</summary>
    Exists,

    /// <summary>No such repo, or it's private and the token can't see it → 404.</summary>
    NotFound,

    /// <summary>The token is missing/expired or lacks the <c>repo</c> scope → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    Failed
}
