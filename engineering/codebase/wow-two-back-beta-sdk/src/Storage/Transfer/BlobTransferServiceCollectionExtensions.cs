using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Transfer;

/// <summary>Registers HMAC-signed transfer URLs and maps the routes that serve them.</summary>
public static class BlobTransferServiceCollectionExtensions
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>
    /// Issues blob URLs through the SDK's own transfer routes, over whichever <see cref="IBlobRepository"/> is registered;
    /// replaces a provider's native issuer. Options come from <paramref name="configure"/>, then <c>Storage:Transfer</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Base URL, signing key and upload cap.</param>
    public static IServiceCollection AddBlobTransferUrls(this IServiceCollection services, Action<BlobTransferOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            BlobTransferOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.BaseUrl is { IsAbsoluteUri: true }, "BlobTransferOptions.BaseUrl must be an absolute URL.")
                .Validate(o => o.SigningKey.Length >= 32, "BlobTransferOptions.SigningKey needs at least 32 characters.")
                .Validate(o => o.MaxUploadBytes > 0, "BlobTransferOptions.MaxUploadBytes must be positive."));
        services.TryAddSingleton(TimeProvider.System);
        services.Replace(ServiceDescriptor.Singleton<IBlobUrlIssuer, HmacBlobUrlIssuer>());
        return services;
    }

    /// <summary>
    /// Maps <c>GET {prefix}/{**path}</c> (download) and <c>PUT {prefix}/{**path}</c> (upload) for URLs from
    /// <see cref="HmacBlobUrlIssuer"/>. An altered or expired URL answers 403, an upload of another media type 403,
    /// one over the cap 413. Point <c>Storage:Transfer:BaseUrl</c> at this prefix.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="prefix">The route prefix. Default <c>files</c>.</param>
    public static RouteGroupBuilder MapBlobTransferEndpoints(this IEndpointRouteBuilder endpoints, string prefix = "files")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(prefix).DisableAntiforgery();
        group.MapGet("{**path}", async (string path, long? exp, string? sig, string? name, HttpContext http, IBlobRepository blobs, CancellationToken cancellationToken) =>
        {
            var normalized = Authorize(http, "GET", path, exp, sig, name);
            var stream = await blobs.OpenReadAsync(normalized, cancellationToken);
            if (stream is null)
                return Results.NotFound();

            var contentType = ContentTypes.TryGetContentType(name ?? normalized, out var guessed) ? guessed : "application/octet-stream";
            if (name is not null)
                http.Response.Headers[HeaderNames.ContentDisposition] = BlobUrlLifetimeExtensions.Attachment(name);

            return Results.Stream(stream, contentType);
        });
        group.MapPut("{**path}", async (string path, long? exp, string? sig, string? type, HttpContext http, IBlobRepository blobs, IOptionsMonitor<BlobTransferOptions> options, CancellationToken cancellationToken) =>
        {
            var normalized = Authorize(http, "PUT", path, exp, sig, type);
            if (type is not null && !string.Equals(MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var sent) ? sent.MediaType.Value : null, type, StringComparison.OrdinalIgnoreCase))
                throw Forbidden("The upload's media type is not the one the URL was issued for.").ToException();

            var limit = options.CurrentValue.MaxUploadBytes;
            if (http.Request.ContentLength > limit)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            await using var capped = new CappedStream(http.Request.Body, limit);
            try
            {
                await blobs.SaveAsync(normalized, capped, type ?? http.Request.ContentType, cancellationToken);
            }
            catch (InvalidDataException)
            {
                await blobs.DeleteAsync(normalized, CancellationToken.None);
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            return Results.Ok();
        });
        return group;
    }

    /// <summary>The normalized path of a request whose signature and expiry hold; otherwise a 403.</summary>
    private static string Authorize(HttpContext http, string method, string path, long? expires, string? signature, string? extra)
    {
        string normalized;
        try
        {
            normalized = BlobStoragePathMapper.Normalize(path);
        }
        catch (ArgumentException)
        {
            throw Forbidden("The transfer URL is not valid.").ToException();
        }

        var services = http.RequestServices;
        var key = services.GetRequiredService<IOptionsMonitor<BlobTransferOptions>>().CurrentValue.SigningKey;
        if (expires is not { } exp
            || exp <= services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds()
            || !BlobTransferSignatureMapper.Matches(key, method, normalized, exp, extra, signature))
        {
            throw Forbidden("The transfer URL is not valid or has expired.").ToException();
        }

        return normalized;
    }

    private static AppError Forbidden(string message)
        => AppError.Of(AppErrorType.Forbidden, message, new Dictionary<string, object?>(StringComparer.Ordinal) { ["messageKey"] = "BlobTransferDenied" });

    /// <summary>Represents a read-only view of a stream that throws <see cref="InvalidDataException"/> past its cap.</summary>
    private sealed class CappedStream(Stream inner, long limit) : Stream
    {
        private long _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            _read += read;
            return _read > limit ? throw new InvalidDataException($"The upload is over the {limit.ToString(CultureInfo.InvariantCulture)}-byte limit.") : read;
        }
    }
}
