namespace WoW.Two.Sdk.Backend.Beta.Web.Idempotency;

/// <summary>
/// Holds the <c>HttpIdempotency</c> configuration section: whether unsafe requests carrying an <c>Idempotency-Key</c>
/// header execute once and replay their stored response. A missing section leaves requests untouched.
/// </summary>
public sealed record HttpIdempotencySettings
{
    /// <summary>The configuration section the settings bind from.</summary>
    public const string SectionName = "HttpIdempotency";

    /// <summary>Honour the idempotency header. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>The request header carrying the key. Default <c>Idempotency-Key</c>.</summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>Methods the header applies to. Default POST and PATCH.</summary>
    public List<string> Methods { get; } = ["POST", "PATCH"];

    /// <summary>How long a stored response replays. Default 24 hours.</summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Largest response stored for replay; larger or streamed responses execute again on retry. Default 1 MiB.</summary>
    public int MaxBodyBytes { get; set; } = 1024 * 1024;
}
