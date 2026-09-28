namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Holds the limits and fonts that steer PDF processing.</summary>
/// <remarks>Set in code with <c>AddPdfProcessing(o => …)</c> or in the host section <c>Media:Pdf</c>, which is applied last.</remarks>
public sealed record PdfOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Media:Pdf";

    /// <summary>Gets or sets the largest input accepted, in bytes. Default 100 MB.</summary>
    public long MaxInputBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>Gets or sets the most pages an input or result may have. Default 2000.</summary>
    public int MaxPages { get; set; } = 2000;

    /// <summary>Gets or sets the font family stamps use when they name none; null takes a sans-serif the platform has.</summary>
    public string? DefaultFontFamily { get; set; }

    /// <summary>Gets font files by family name; they win over system fonts.</summary>
    public Dictionary<string, string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
}
