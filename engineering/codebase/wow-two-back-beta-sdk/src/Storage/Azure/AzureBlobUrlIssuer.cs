using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Specialized;
using global::Azure.Storage.Sas;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Azure;

/// <summary>
/// Issues Azure blob SAS URLs: signed with the account key of a connection string, or with a user delegation key when
/// the container is reached through a token credential.
/// </summary>
/// <param name="container">The container the blobs live in.</param>
/// <param name="options">Key prefix.</param>
/// <param name="time">The clock start and expiry are set from.</param>
public sealed class AzureBlobUrlIssuer(BlobContainerClient container, AzureBlobStorageOptions options, TimeProvider time) : IBlobUrlIssuer
{
    private readonly string _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix) ? string.Empty : BlobStoragePathMapper.Normalize(options.KeyPrefix) + "/";

    /// <inheritdoc />
    public async Task<BlobUrlResult> IssueReadUrlAsync(string path, TimeSpan lifetime, string? downloadFileName = null, CancellationToken cancellationToken = default)
    {
        var (blob, sas) = Builder(path, lifetime);
        sas.SetPermissions(BlobSasPermissions.Read);
        if (downloadFileName is not null)
            sas.ContentDisposition = BlobUrlLifetimeExtensions.Attachment(downloadFileName);

        return new BlobUrlResult { Url = await SignAsync(blob, sas, cancellationToken), Method = "GET", ExpiresAt = sas.ExpiresOn };
    }

    /// <inheritdoc />
    public async Task<BlobUrlResult> IssueWriteUrlAsync(string path, TimeSpan lifetime, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var (blob, sas) = Builder(path, lifetime);
        sas.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["x-ms-blob-type"] = "BlockBlob" };
        if (contentType is not null)
            headers["Content-Type"] = contentType;

        return new BlobUrlResult { Url = await SignAsync(blob, sas, cancellationToken), Method = "PUT", Headers = headers, ExpiresAt = sas.ExpiresOn };
    }

    private (BlobClient Blob, BlobSasBuilder Sas) Builder(string path, TimeSpan lifetime)
    {
        var blob = container.GetBlobClient(_prefix + BlobStoragePathMapper.Normalize(path));
        var now = time.GetUtcNow();
        return (blob, new BlobSasBuilder
        {
            BlobContainerName = container.Name,
            BlobName = blob.Name,
            Resource = "b",
            StartsOn = now.AddMinutes(-5),
            ExpiresOn = now + lifetime.EnsureValid(),
        });
    }

    /// <summary>Signs with the account key when the client holds one, else with a user delegation key.</summary>
    private async Task<Uri> SignAsync(BlobClient blob, BlobSasBuilder sas, CancellationToken cancellationToken)
    {
        if (blob.CanGenerateSasUri)
            return blob.GenerateSasUri(sas);

        var service = container.GetParentBlobServiceClient();
        var key = await service.GetUserDelegationKeyAsync(sas.StartsOn, sas.ExpiresOn, cancellationToken);
        return new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(key.Value, service.AccountName) }.ToUri();
    }
}
