namespace WoW.Two.Sdk.Backend.Beta.Web.Idempotency;

/// <summary>A response stored under an idempotency key, with the fingerprint of the request that produced it.</summary>
public sealed record IdempotentHttpResponseModel
{
    /// <summary>The response status code.</summary>
    public required int StatusCode { get; init; }

    /// <summary>The response content type, if any.</summary>
    public string? ContentType { get; init; }

    /// <summary>The response body.</summary>
    public required byte[] Body { get; init; }

    /// <summary>SHA-256 hex of the original request body; a retry with another body is refused.</summary>
    public required string Fingerprint { get; init; }
}
