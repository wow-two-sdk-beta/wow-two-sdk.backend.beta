namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Represents a rendered email: subject, branded HTML and its plain-text twin.</summary>
public sealed record EmailTemplateResult
{
    /// <summary>Gets the subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the HTML body, styles inlined for mail clients.</summary>
    public required string Html { get; init; }

    /// <summary>Gets the plain-text body, links written out.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the culture whose template was used.</summary>
    public required string Culture { get; init; }
}
