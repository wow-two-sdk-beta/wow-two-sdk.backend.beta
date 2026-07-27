namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>
/// A provider-neutral blob (file object) store — save, read, existence, delete, metadata, and listing over
/// forward-slash logical paths. The default implementation is the local filesystem; cloud adapters
/// (S3, Azure Blob, GCS) implement the same surface so call sites don't change.
/// </summary>
public interface IBlobStorage
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/>, creating or overwriting the blob.</summary>
    /// <param name="path">The logical blob path (forward-slash separated, relative to the store root).</param>
    /// <param name="content">The stream to store; read from its current position to the end.</param>
    /// <param name="contentType">Optional MIME type; stored when the backend supports it, ignored otherwise.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>Opens the blob for reading, or returns <see langword="null"/> when it does not exist.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A readable stream the caller must dispose, or <see langword="null"/> when absent.</returns>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a blob exists at <paramref name="path"/>.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the blob exists.</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Deletes the blob at <paramref name="path"/> if present.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when a blob was deleted; <see langword="false"/> when none existed.</returns>
    Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Returns metadata for the blob at <paramref name="path"/>, or <see langword="null"/> when absent.</summary>
    /// <param name="path">The logical blob path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The blob metadata, or <see langword="null"/>.</returns>
    Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Lists blobs whose path starts with <paramref name="prefix"/> (all blobs when null), recursively.</summary>
    /// <param name="prefix">The path prefix to filter by, or <see langword="null"/> for everything.</param>
    /// <param name="cancellationToken">Token to stop the enumeration.</param>
    /// <returns>An async sequence of blob metadata.</returns>
    IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);
}
