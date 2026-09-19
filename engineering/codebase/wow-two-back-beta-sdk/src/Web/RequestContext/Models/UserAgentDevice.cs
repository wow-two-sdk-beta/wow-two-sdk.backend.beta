namespace WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;

/// <summary>Identifies a heuristic device family from a User-Agent string.</summary>
public enum UserAgentDevice
{
    /// <summary>No supported marker matched.</summary>
    Unknown,
    /// <summary>An automated crawler marker matched.</summary>
    Bot,
    /// <summary>An iOS device marker matched.</summary>
    Ios,
    /// <summary>An Android marker matched.</summary>
    Android,
    /// <summary>A desktop operating-system marker matched.</summary>
    Desktop,
}
