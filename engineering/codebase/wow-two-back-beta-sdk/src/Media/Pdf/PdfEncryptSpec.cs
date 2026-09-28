namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents AES-256 protection: an open password, an owner password and what readers may do.</summary>
public sealed record PdfEncryptSpec
{
    /// <summary>Gets the password needed to open the file; null opens without one but keeps the restrictions.</summary>
    public string? UserPassword { get; init; }

    /// <summary>Gets the password that lifts every restriction.</summary>
    public required string OwnerPassword { get; init; }

    /// <summary>Gets whether readers may print. Default true.</summary>
    public bool AllowPrinting { get; init; } = true;

    /// <summary>Gets whether readers may copy text and images. Default false.</summary>
    public bool AllowCopying { get; init; }

    /// <summary>Gets whether readers may change the content. Default false.</summary>
    public bool AllowEditing { get; init; }

    /// <summary>Gets whether readers may add comments. Default false.</summary>
    public bool AllowAnnotations { get; init; }

    /// <summary>Gets whether readers may fill in forms. Default true.</summary>
    public bool AllowFormFilling { get; init; } = true;
}
