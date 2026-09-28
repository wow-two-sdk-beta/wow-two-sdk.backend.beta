using System.Runtime.CompilerServices;
using global::Azure;
using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using BlobInfo = WoW.Two.Sdk.Backend.Beta.Storage.Core.BlobInfo;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Azure;

/// <summary>Accesses blobs in one Azure Blob Storage container; paths map to blob names under the optional key prefix.</summary>
public sealed class AzureBlobRepository : IBlobRepository, IDisposable
{
    private readonly BlobContainerClient _container;
    private readonly bool _createContainer;
    private readonly string _prefix;
    private readonly SemaphoreSlim _containerGate = new(1, 1);
    private bool _containerReady;

    /// <summary>Create the repository over a container client.</summary>
    /// <param name="container">The container the blobs live in.</param>
    /// <param name="options">Key prefix and container creation.</param>
    public AzureBlobRepository(BlobContainerClient container, AzureBlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(options);
        _container = container;
        _createContainer = options.CreateContainerIfMissing;
        _prefix = string.IsNullOrWhiteSpace(options.KeyPrefix) ? string.Empty : BlobStoragePathMapper.Normalize(options.KeyPrefix) + "/";
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var blob = BlobOf(path);
        await EnsureContainerAsync(cancellationToken);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" } },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await BlobOf(path).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => (await BlobOf(path).ExistsAsync(cancellationToken)).Value;

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
        => (await BlobOf(path).DeleteIfExistsAsync(cancellationToken: cancellationToken)).Value;

    /// <inheritdoc />
    public async Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = BlobOf(path);
        try
        {
            var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
            return new BlobInfo
            {
                Path = blob.Name[_prefix.Length..],
                SizeBytes = properties.ContentLength,
                LastModified = properties.LastModified,
                ContentType = properties.ContentType,
            };
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var namePrefix = _prefix + (BlobStoragePathMapper.NormalizePrefix(prefix) ?? string.Empty);
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, namePrefix, cancellationToken))
        {
            yield return new BlobInfo
            {
                Path = item.Name[_prefix.Length..],
                SizeBytes = item.Properties.ContentLength ?? 0,
                LastModified = item.Properties.LastModified ?? DateTimeOffset.MinValue,
                ContentType = item.Properties.ContentType,
            };
        }
    }

    /// <inheritdoc />
    public void Dispose() => _containerGate.Dispose();

    private BlobClient BlobOf(string path) => _container.GetBlobClient(_prefix + BlobStoragePathMapper.Normalize(path));

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (!_createContainer || _containerReady)
            return;

        await _containerGate.WaitAsync(cancellationToken);
        try
        {
            if (!_containerReady)
            {
                await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                _containerReady = true;
            }
        }
        finally
        {
            _containerGate.Release();
        }
    }
}
