namespace WoW.Two.Sdk.Backend.Beta.Integrations.GitHub;

/// <summary>Refers to outcome of probing for a file at a path in a repo → tells whether a marker/config file is present.</summary>
public enum FileCheck
{
    /// <summary>The file exists at the requested path → 200.</summary>
    Present,

    /// <summary>No such file at the path → 404.</summary>
    Absent,

    /// <summary>The token is missing/expired or can't see the repo → 401/403.</summary>
    Unauthorized,

    /// <summary>The check could not be completed (transport error or an unexpected status).</summary>
    Failed
}
