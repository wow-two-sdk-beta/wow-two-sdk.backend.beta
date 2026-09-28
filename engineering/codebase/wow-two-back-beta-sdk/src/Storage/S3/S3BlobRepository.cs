using System.Net;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.S3;

/// <summary>
/// Accesses blobs in one S3-compatible bucket. Paths map to object keys under the optional key prefix; a
/// non-seekable upload is spooled to a temporary file so the object length is known before sending.
/// </summary>
/// <param name="s3">The S3 client.</param>
/// <param name="options">Bucket and key prefix.</param>
public sealed class S3BlobRepository(IAmazonS3 s3, S3BlobStorageOptions options) : IBlobRepository
{
    private readonly string _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix) ? string.Empty : BlobStoragePathMapper.Normalize(options.KeyPrefix) + "/";

    /// <inheritdoc />
    public async Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var key = KeyOf(path);

        if (content.CanSeek)
        {
            await PutAsync(key, content, contentType, cancellationToken);
            return;
        }

        await using var spool = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await content.CopyToAsync(spool, cancellationToken);
        spool.Position = 0;
        await PutAsync(key, spool, contentType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3.GetObjectAsync(options.BucketName, KeyOf(path), cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => await GetInfoAsync(path, cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(path, cancellationToken))
            return false;

        await s3.DeleteObjectAsync(options.BucketName, KeyOf(path), cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        var key = KeyOf(path);
        try
        {
            var metadata = await s3.GetObjectMetadataAsync(options.BucketName, key, cancellationToken);
            return new BlobInfo
            {
                Path = key[_prefix.Length..],
                SizeBytes = metadata.ContentLength,
                LastModified = ToOffset(metadata.LastModified),
                ContentType = metadata.Headers.ContentType,
            };
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request { BucketName = options.BucketName, Prefix = _prefix + (BlobStoragePathMapper.NormalizePrefix(prefix) ?? string.Empty) };
        ListObjectsV2Response page;
        do
        {
            page = await s3.ListObjectsV2Async(request, cancellationToken);
            foreach (var entry in page.S3Objects ?? [])
            {
                yield return new BlobInfo
                {
                    Path = entry.Key[_prefix.Length..],
                    SizeBytes = entry.Size,
                    LastModified = ToOffset(entry.LastModified),
                };
            }

            request.ContinuationToken = page.NextContinuationToken;
        }
        while (page.IsTruncated == true);
    }

    private Task<PutObjectResponse> PutAsync(string key, Stream body, string? contentType, CancellationToken cancellationToken)
        => s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.BucketName,
                Key = key,
                InputStream = body,
                AutoCloseStream = false,
                ContentType = contentType ?? "application/octet-stream",
            },
            cancellationToken);

    private string KeyOf(string path) => _prefix + BlobStoragePathMapper.Normalize(path);

    private static DateTimeOffset ToOffset(DateTime value)
        => new(DateTime.SpecifyKind(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value, DateTimeKind.Utc));
}
