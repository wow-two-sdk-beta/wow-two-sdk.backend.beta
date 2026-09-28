namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Represents an approved WhatsApp template and the values it is sent with.</summary>
public sealed record WhatsAppTemplate
{
    /// <summary>The template name (Meta) or content SID (<c>HX…</c>, Twilio).</summary>
    public required string Name { get; init; }

    /// <summary>The template's language code, such as <c>en_US</c> or <c>ru</c>. Default <c>en_US</c>.</summary>
    public string LanguageCode { get; init; } = "en_US";

    /// <summary>Values for the body placeholders <c>{{1}}</c>, <c>{{2}}</c>, … in order.</summary>
    public IReadOnlyList<string> BodyParameters { get; init; } = [];

    /// <summary>The value of the first button's placeholder — the code, for an authentication template's copy-code button.</summary>
    public string? ButtonParameter { get; init; }
}
