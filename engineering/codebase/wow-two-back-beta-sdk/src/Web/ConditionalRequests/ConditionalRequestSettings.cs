namespace WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;

/// <summary>
/// Holds the <c>ConditionalRequests</c> configuration section: whether successful GET responses get an entity tag and
/// matching <c>If-None-Match</c> requests a <c>304</c>. A missing section leaves responses untouched.
/// </summary>
public sealed record ConditionalRequestSettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "ConditionalRequests";

    /// <summary>Tag GET responses and answer revalidations with <c>304</c>. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Largest body that is buffered and hashed; larger or flushed responses stream untagged. Default 1 MiB.</summary>
    public int MaxBufferBytes { get; set; } = 1024 * 1024;
}
