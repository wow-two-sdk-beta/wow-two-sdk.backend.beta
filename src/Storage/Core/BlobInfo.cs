namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>Metadata about a stored blob — its logical path, size, last-modified time, and (when known) content type.</summary>
public sealed record BlobInfo
{
    /// <summary>Gets the blob's logical path (forward-slash separated, relative to the store root).</summary>
    public required string Path { get; init; }

    /// <summary>Gets the blob size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Gets the last time the blob was modified (UTC).</summary>
    public required DateTimeOffset LastModified { get; init; }

    /// <summary>Gets the content type when the backing store records one; <see langword="null"/> otherwise (e.g. the local file store).</summary>
    public string? ContentType { get; init; }
}
