namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Holds the brand every templated email wears: name or logo, accent color and footer.</summary>
public sealed record EmailBrandOptions
{
    /// <summary>Gets or sets the product name, shown when there is no logo and in the footer. Default <c>App</c>.</summary>
    public string Name { get; set; } = "App";

    /// <summary>Gets or sets an absolute URL of the logo image, shown at the top; null shows the name.</summary>
    public Uri? LogoUrl { get; set; }

    /// <summary>Gets or sets the accent color of links and buttons, as <c>#RRGGBB</c>. Default indigo.</summary>
    public string AccentColor { get; set; } = "#4F46E5";

    /// <summary>Gets or sets the footer line, such as the company address; null shows the name only.</summary>
    public string? FooterText { get; set; }

    /// <summary>Gets or sets the product's website, linked from the footer.</summary>
    public Uri? WebsiteUrl { get; set; }
}
