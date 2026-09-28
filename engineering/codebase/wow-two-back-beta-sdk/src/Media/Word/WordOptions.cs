namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Holds the limits and typography that steer Word processing.</summary>
/// <remarks>Set in code with <c>AddWordProcessing(o => …)</c> or in the host section <c>Media:Word</c>, which is applied last.</remarks>
public sealed record WordOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Media:Word";

    /// <summary>Gets or sets the largest input accepted, in bytes. Default 50 MB.</summary>
    public long MaxInputBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Gets or sets the most characters one document part may hold, a guard against XML bombs. Default 32 million.</summary>
    public long MaxCharactersPerPart { get; set; } = 32_000_000;

    /// <summary>Gets or sets the body font of built documents. Default Calibri.</summary>
    public string FontFamily { get; set; } = "Calibri";

    /// <summary>Gets or sets the body font size of built documents, in points. Default 11.</summary>
    public double FontSizePoints { get; set; } = 11;

    /// <summary>Gets or sets the font of code in built documents. Default Consolas.</summary>
    public string CodeFontFamily { get; set; } = "Consolas";
}
