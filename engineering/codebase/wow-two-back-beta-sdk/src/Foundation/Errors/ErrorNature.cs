namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Refers to the descriptive nature of a failure — consumers derive retry, fallback, and log decisions from it.</summary>
public enum ErrorNature
{
    /// <summary>Temporary; retrying may succeed (timeout, 503, 429).</summary>
    Transient,

    /// <summary>Stable; retrying the same call will not help (validation, 404, 401).</summary>
    Permanent,

    /// <summary>An internal bug — our fault, never retried (unexpected, deserialization).</summary>
    Defect,
}
