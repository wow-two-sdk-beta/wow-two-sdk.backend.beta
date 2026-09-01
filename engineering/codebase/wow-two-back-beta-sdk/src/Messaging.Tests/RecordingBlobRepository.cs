using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// An <see cref="IBlobRepository"/> that keeps blobs in memory and records every call, so a test can assert both what was
/// stored and that nothing was stored at all.
/// </summary>
/// <remarks>
///   - call counts prove the store was never touched while the feature is off
///   - <see cref="DropWrites"/> reports success
///   - keeps nothing, so a read finds a purged blob without a sweep or a sleep
/// </remarks>
internal sealed class RecordingBlobRepository : IBlobRepository
{
    private readonly ConcurrentDictionary<string, BlobEntry> _blobs = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _calls = new();

    /// <summary>Accept writes and store nothing — the blob a later read cannot find.</summary>
    public bool DropWrites { get; set; }

    /// <summary>Every call made against this store, as <c>verb path</c>, in order.</summary>
    public IReadOnlyList<string> Calls => [.. _calls];

    /// <summary>How many blobs are actually held.</summary>
    public int BlobCount => _blobs.Count;

    /// <summary>Calls that read a blob or its metadata — what a rehydrate costs.</summary>
    public int ReadCalls => _calls.Count(call => call.StartsWith("GetInfo ", StringComparison.Ordinal) || call.StartsWith("OpenRead ", StringComparison.Ordinal));

    /// <summary>Calls that wrote a blob.</summary>
    public int SaveCalls => _calls.Count(call => call.StartsWith("Save ", StringComparison.Ordinal));

    /// <summary>The stored bytes at <paramref name="path"/>, or null when nothing is there.</summary>
    public byte[]? Read(string path) => _blobs.TryGetValue(path, out var entry) ? entry.Content : null;

    public Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Save " + path);
        if (DropWrites)
            return Task.CompletedTask;

        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _blobs[path] = new BlobEntry(buffer.ToArray(), contentType, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("OpenRead " + path);
        return Task.FromResult<Stream?>(_blobs.TryGetValue(path, out var entry) ? new MemoryStream(entry.Content, writable: false) : null);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Exists " + path);
        return Task.FromResult(_blobs.ContainsKey(path));
    }

    public Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Delete " + path);
        return Task.FromResult(_blobs.TryRemove(path, out _));
    }

    public Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("GetInfo " + path);
        return Task.FromResult(_blobs.TryGetValue(path, out var entry)
            ? new BlobInfo { Path = path, SizeBytes = entry.Content.LongLength, LastModified = entry.LastModified, ContentType = entry.ContentType }
            : null);
    }

    public async IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // an in-memory listing has nothing to await; the iterator still has to be async

        _calls.Enqueue("List " + (prefix ?? string.Empty));
        foreach (var (path, entry) in _blobs)
        {
            if (prefix is not null && !path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            yield return new BlobInfo { Path = path, SizeBytes = entry.Content.LongLength, LastModified = entry.LastModified, ContentType = entry.ContentType };
        }
    }

    private sealed record BlobEntry(byte[] Content, string? ContentType, DateTimeOffset LastModified);
}
