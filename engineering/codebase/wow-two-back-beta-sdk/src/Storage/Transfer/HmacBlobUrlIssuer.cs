using System.Globalization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Transfer;

/// <summary>
/// Issues URLs of the SDK's transfer routes (<c>MapBlobTransferEndpoints</c>), signed with HMAC-SHA256 over the method,
/// path, expiry and file name or media type. Works over any <see cref="IBlobRepository"/>, the local disk included.
/// </summary>
/// <param name="options">Base URL and key; <c>Storage:Transfer</c> reloads live.</param>
/// <param name="time">The clock expiry is set from.</param>
public sealed class HmacBlobUrlIssuer(IOptionsMonitor<BlobTransferOptions> options, TimeProvider time) : IBlobUrlIssuer
{
    /// <inheritdoc />
    public Task<BlobUrlResult> IssueReadUrlAsync(string path, TimeSpan lifetime, string? downloadFileName = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Issue("GET", path, lifetime, "name", downloadFileName, new Dictionary<string, string>()));

    /// <inheritdoc />
    public Task<BlobUrlResult> IssueWriteUrlAsync(string path, TimeSpan lifetime, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (contentType is not null)
            headers["Content-Type"] = contentType;

        return Task.FromResult(Issue("PUT", path, lifetime, "type", contentType, headers));
    }

    private BlobUrlResult Issue(string method, string path, TimeSpan lifetime, string extraName, string? extra, Dictionary<string, string> headers)
    {
        var current = options.CurrentValue;
        var baseUrl = current.BaseUrl ?? throw new InvalidOperationException("Set Storage:Transfer:BaseUrl to issue transfer URLs.");
        var normalized = BlobStoragePathMapper.Normalize(path);
        var expiresAt = time.GetUtcNow() + lifetime.EnsureValid();
        var expires = expiresAt.ToUnixTimeSeconds();
        var query = $"exp={expires.ToString(CultureInfo.InvariantCulture)}&sig={BlobTransferSignatureMapper.Sign(current.SigningKey, method, normalized, expires, extra)}";
        if (extra is not null)
            query += $"&{extraName}={Uri.EscapeDataString(extra)}";

        var escapedPath = string.Join('/', normalized.Split('/').Select(Uri.EscapeDataString));
        var url = new Uri($"{baseUrl.ToString().TrimEnd('/')}/{escapedPath}?{query}");
        return new BlobUrlResult { Url = url, Method = method, Headers = headers, ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expires) };
    }
}
