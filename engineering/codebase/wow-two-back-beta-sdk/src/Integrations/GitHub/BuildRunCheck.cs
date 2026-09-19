namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Refers to outcome of reading the latest run of a workflow — distinguishes succeeded / failed / running / never-ran / unreadable.</summary>
public enum BuildRunCheck
{
    /// <summary>The latest run completed successfully.</summary>
    Succeeded,

    /// <summary>The latest run completed unsuccessfully (failed, cancelled, or timed out).</summary>
    Failed,

    /// <summary>A run is queued or in progress.</summary>
    Running,

    /// <summary>The workflow has no runs yet.</summary>
    None,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    ProbeFailed
}
