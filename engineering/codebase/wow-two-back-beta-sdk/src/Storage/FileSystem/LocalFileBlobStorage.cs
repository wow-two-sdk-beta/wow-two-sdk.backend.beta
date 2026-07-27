using System.Runtime.CompilerServices;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;

/// <summary>
/// <see cref="IBlobStorage"/> over the local filesystem, rooted at a configured directory. Logical blob
/// paths map to files under the root; all access is traversal-guarded so a path can never escape the root.
/// Content types are not persisted (the local file store has nowhere to keep them). Thread-safe.
/// </summary>
public sealed class LocalFileBlobStorage : IBlobStorage
{
    private readonly string _root;

    /// <summary>Creates the store rooted at <paramref name="rootPath"/>, creating the directory if needed.</summary>
    /// <param name="rootPath">The base directory under which blobs are stored.</param>
    public LocalFileBlobStorage(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = BlobStoragePath.ResolveUnderRoot(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = BlobStoragePath.ResolveUnderRoot(_root, path);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(BlobStoragePath.ResolveUnderRoot(_root, path)));

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = BlobStoragePath.ResolveUnderRoot(_root, path);
        if (!File.Exists(fullPath)) return Task.FromResult(false);

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = BlobStoragePath.ResolveUnderRoot(_root, path);
        return Task.FromResult(File.Exists(fullPath) ? ToBlobInfo(new FileInfo(fullPath)) : null);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_root))
        {
            var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : BlobStoragePath.Normalize(prefix);

            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var info = ToBlobInfo(new FileInfo(file));
                if (normalizedPrefix is null || info.Path.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                    yield return info;
            }
        }

        await Task.CompletedTask;
    }

    private BlobInfo ToBlobInfo(FileInfo info)
        => new()
        {
            Path = Path.GetRelativePath(_root, info.FullName).Replace('\\', '/'),
            SizeBytes = info.Length,
            LastModified = info.LastWriteTimeUtc,
        };
}
