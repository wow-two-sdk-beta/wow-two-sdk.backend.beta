namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Holds one template in one culture: a subject line and a Markdown body, both with <c>{placeholders}</c>.</summary>
public sealed record EmailTemplateContentOptions
{
    /// <summary>Gets or sets the subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the Markdown body; a lone link titled <c>"button"</c> becomes a button.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the preview line inboxes show after the subject; null shows the body's start.</summary>
    public string? Preheader { get; set; }
}
