namespace WoW.Two.Sdk.Backend.Beta.Storage.Transfer;

/// <summary>Holds where the SDK's transfer routes are reachable and the key that signs their URLs.</summary>
/// <remarks>Set in code with <c>AddBlobTransferUrls(o => …)</c> or in the host section <c>Storage:Transfer</c>, which is applied last.</remarks>
public sealed record BlobTransferOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Storage:Transfer";

    /// <summary>Gets or sets the public URL of the mapped transfer routes, such as <c>https://api.example/files</c>.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>Gets or sets the signing key, at least 32 characters; rotating it voids every issued URL.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the largest upload accepted, in bytes. Default 100 MB.</summary>
    public long MaxUploadBytes { get; set; } = 100L * 1024 * 1024;
}
