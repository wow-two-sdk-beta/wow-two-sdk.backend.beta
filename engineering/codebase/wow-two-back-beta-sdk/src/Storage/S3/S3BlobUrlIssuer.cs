using Amazon.S3;
using Amazon.S3.Model;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.S3;

/// <summary>Issues SigV4 presigned S3 URLs; they are signed locally, with no call to the service.</summary>
/// <param name="s3">The S3 client holding the credentials.</param>
/// <param name="options">Bucket, key prefix and endpoint.</param>
/// <param name="time">The clock expiry is set from.</param>
public sealed class S3BlobUrlIssuer(IAmazonS3 s3, S3BlobStorageOptions options, TimeProvider time) : IBlobUrlIssuer
{
    private readonly string _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix) ? string.Empty : BlobStoragePathMapper.Normalize(options.KeyPrefix) + "/";

    /// <inheritdoc />
    public Task<BlobUrlResult> IssueReadUrlAsync(string path, TimeSpan lifetime, string? downloadFileName = null, CancellationToken cancellationToken = default)
    {
        var request = Request(path, lifetime, HttpVerb.GET);
        if (downloadFileName is not null)
            request.ResponseHeaderOverrides.ContentDisposition = BlobUrlLifetimeExtensions.Attachment(downloadFileName);

        return Task.FromResult(new BlobUrlResult { Url = new Uri(s3.GetPreSignedURL(request)), Method = "GET", ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(request.Expires, DateTimeKind.Utc)) });
    }

    /// <inheritdoc />
    public Task<BlobUrlResult> IssueWriteUrlAsync(string path, TimeSpan lifetime, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var request = Request(path, lifetime, HttpVerb.PUT);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (contentType is not null)
        {
            request.ContentType = contentType;
            headers["Content-Type"] = contentType;
        }

        return Task.FromResult(new BlobUrlResult { Url = new Uri(s3.GetPreSignedURL(request)), Method = "PUT", Headers = headers, ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(request.Expires, DateTimeKind.Utc)) });
    }

    private GetPreSignedUrlRequest Request(string path, TimeSpan lifetime, HttpVerb verb) => new()
    {
        BucketName = options.BucketName,
        Key = _prefix + BlobStoragePathMapper.Normalize(path),
        Verb = verb,
        Expires = (time.GetUtcNow() + lifetime.EnsureValid()).UtcDateTime,
        Protocol = options.ServiceUrl?.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS,
    };
}
