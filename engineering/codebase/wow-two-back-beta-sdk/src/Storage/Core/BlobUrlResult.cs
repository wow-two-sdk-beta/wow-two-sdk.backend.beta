namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>Represents a signed URL a client uses directly: the method, the headers it must send, and the expiry.</summary>
public sealed record BlobUrlResult
{
    /// <summary>Gets the signed URL.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the HTTP method: <c>GET</c> to download, <c>PUT</c> to upload.</summary>
    public required string Method { get; init; }

    /// <summary>Gets the headers the request must carry, such as <c>Content-Type</c> or Azure's <c>x-ms-blob-type</c>.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets when the URL stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
