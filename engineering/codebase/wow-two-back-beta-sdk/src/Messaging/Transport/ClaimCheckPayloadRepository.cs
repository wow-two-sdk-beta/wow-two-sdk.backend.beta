using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Reads and writes claim-checked bodies through the SDK's <see cref="IBlobRepository"/>, owning the path scheme and the guards on a reference that arrived over the wire.</summary>
internal sealed class ClaimCheckPayloadRepository(IBlobRepository blobRepository, ClaimCheckOptions options, TimeProvider timeProvider)
{
    private readonly ClaimCheckOptions _options = options;

    /// <summary>The normalized prefix every claim-checked blob lives under, with no trailing separator.</summary>
    public string Prefix { get; } = BlobStoragePathMapper.Normalize(options.PathPrefix).TrimEnd('/');

    /// <summary>Write a serialized body to blob storage and return the logical path to reference it by.</summary>
    /// <param name="body">The serialized body.</param>
    /// <param name="contentType">The serializer's content type, recorded where the backing store supports it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<string> WriteAsync(byte[] body, string contentType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Date segments keep one directory bounded and let prefix-scoped lifecycle rules expire whole days.
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}/{now:yyyy'/'MM'/'dd}/{Guid.NewGuid():N}.bin");

        using var stream = new MemoryStream(body, writable: false);
        await blobRepository.SaveAsync(path, stream, contentType, cancellationToken);
        return path;
    }

    /// <summary>Read an offloaded body back, or fail with a reason fit to dead-letter on.</summary>
    /// <param name="path">The claim reference, as it arrived on the wire.</param>
    /// <param name="messageId">The message the reference came from, for the failure message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<Result<byte[]>> ReadAsync(string path, string messageId, CancellationToken cancellationToken)
    {
        var confined = Confine(path, messageId);
        if (confined is Result<string>.Failure rejected)
        {
            return Result<byte[]>.Fail(rejected.Error);
        }

        var safePath = ((Result<string>.Success)confined).Value;

        // Metadata first — separates "gone" from "too big" before a byte is allocated, and gives the exact length.
        var info = await blobRepository.GetInfoAsync(safePath, cancellationToken);
        if (info is null)
        {
            return Result<byte[]>.Fail(Missing(safePath, messageId));
        }

        if (info.SizeBytes > _options.MaxPayloadBytes)
        {
            return Result<byte[]>.Fail(AppErrorFactory.DataIntegrity(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Claim-checked body '{safePath}' for message '{messageId}' is {info.SizeBytes} bytes, over the {_options.MaxPayloadBytes}-byte rehydrate limit; refusing to load it.")));
        }

        var stream = await blobRepository.OpenReadAsync(safePath, cancellationToken);
        if (stream is null)
        {
            return Result<byte[]>.Fail(Missing(safePath, messageId)); // deleted between the two calls — a retention sweep racing a slow consumer
        }

        await using (stream)
        {
            var buffer = new byte[info.SizeBytes];
            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                return Result<byte[]>.Fail(AppErrorFactory.DataIntegrity(
                    $"Claim-checked body '{safePath}' for message '{messageId}' is shorter than its recorded length; the blob is truncated.",
                    exception));
            }

            return Result<byte[]>.Ok(buffer);
        }
    }

    /// <summary>Delete every claim-checked blob last modified before <paramref name="cutoffUtc"/>, up to <paramref name="maxDeletes"/> in one pass.</summary>
    /// <param name="cutoffUtc">Blobs older than this are expired.</param>
    /// <param name="maxDeletes">Ceiling on one pass, so a first sweep over a large store does not run unbounded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many blobs were deleted.</returns>
    public async ValueTask<int> SweepAsync(DateTimeOffset cutoffUtc, int maxDeletes, CancellationToken cancellationToken)
    {
        // Collect before deleting — mutating the store mid-listing throws on a directory-backed one.
        var expired = new List<string>();
        await foreach (var blob in blobRepository.ListAsync(Prefix, cancellationToken))
        {
            if (blob.LastModified >= cutoffUtc)
                continue;

            expired.Add(blob.Path);
            if (expired.Count >= maxDeletes)
                break; // the rest keep until the next pass
        }

        var deleted = 0;
        foreach (var blobPath in expired)
            if (await blobRepository.DeleteAsync(blobPath, cancellationToken))
                deleted++;

        return deleted;
    }

    // A reference is wire data — normalize, then confine to the prefix, so a forged header cannot read arbitrary blobs.
    private Result<string> Confine(string path, string messageId)
    {
        string normalized;
        try
        {
            normalized = BlobStoragePathMapper.Normalize(path);
        }
        catch (ArgumentException)
        {
            return Result<string>.Fail(AppErrorFactory.Validation(
                $"Claim-check reference '{path}' on message '{messageId}' is not a valid blob path."));
        }

        if (!normalized.StartsWith(Prefix + "/", StringComparison.Ordinal))
        {
            return Result<string>.Fail(AppErrorFactory.Validation(
                $"Claim-check reference '{path}' on message '{messageId}' points outside the '{Prefix}' prefix; refusing to read it."));
        }

        return Result<string>.Ok(normalized);
    }

    private static AppError Missing(string path, string messageId) => AppErrorFactory.FileNotFound(
        $"Claim-checked body '{path}' for message '{messageId}' is missing from blob storage — it expired under the configured retention, was purged, or was never written. The body cannot be rehydrated.");
}
