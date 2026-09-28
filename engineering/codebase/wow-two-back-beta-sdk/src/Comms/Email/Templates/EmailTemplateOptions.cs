namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Holds the email templates by name and culture, and the brand they wear.</summary>
/// <remarks>Set in code with <c>AddEmailTemplates(o => …)</c> or in the host section <c>Comms:Email:Templates</c>, which is applied last.</remarks>
public sealed record EmailTemplateOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Comms:Email:Templates";

    /// <summary>Gets or sets the culture used when the requested one and its parents have no template. Default <c>en</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Gets the brand.</summary>
    public EmailBrandOptions Brand { get; } = new();

    /// <summary>Gets the templates: name, then culture, such as <c>welcome</c> → <c>en</c>; both ignore case.</summary>
    public Dictionary<string, Dictionary<string, EmailTemplateContentOptions>> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);
}
