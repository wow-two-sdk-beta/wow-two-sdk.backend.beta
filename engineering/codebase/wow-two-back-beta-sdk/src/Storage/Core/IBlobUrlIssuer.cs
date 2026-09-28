namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>
/// Defines signed URLs for one blob, so browsers and apps upload and download directly instead of through the API.
/// The storage provider issues them (S3 presigning, Azure SAS); otherwise the SDK's HMAC-signed transfer routes do.
/// </summary>
public interface IBlobUrlIssuer
{
    /// <summary>Issues a URL that downloads the blob until it expires.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="lifetime">How long the URL works, up to 7 days.</param>
    /// <param name="downloadFileName">A file name the browser saves as; null keeps the provider's default.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<BlobUrlResult> IssueReadUrlAsync(string path, TimeSpan lifetime, string? downloadFileName = null, CancellationToken cancellationToken = default);

    /// <summary>Issues a URL that uploads the blob with <c>PUT</c> until it expires.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="lifetime">How long the URL works, up to 7 days.</param>
    /// <param name="contentType">The media type the upload must declare; null accepts any.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<BlobUrlResult> IssueWriteUrlAsync(string path, TimeSpan lifetime, string? contentType = null, CancellationToken cancellationToken = default);
}
