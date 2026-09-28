namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

/// <summary>Represents the code, recipient and context for an OTP delivery.</summary>
public sealed record OtpDeliveryEnvelopeModel
{
    /// <summary>Channel-specific recipient — Telegram chat id, phone number, email address.</summary>
    public required string DeliveryAddress { get; init; }

    /// <summary>The code to deliver.</summary>
    public required string Code { get; init; }

    /// <summary>Scope the code was created for (drives message wording).</summary>
    public required string Scope { get; init; }

    /// <summary>Optional channel-specific extras.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>The worded message, usually from <see cref="IOtpMessageFormatter"/>; null lets the handler word it.</summary>
    public string? Text { get; init; }

    /// <summary>The subject line for channels that carry one (email); null lets the handler word it.</summary>
    public string? Subject { get; init; }

    /// <summary>The broker of the channel to send through, such as <c>eskiz</c>; null takes the channel's default.</summary>
    public string? Broker { get; init; }

    /// <summary>How long the code stays valid; null takes <see cref="OtpOptions.CodeLifetime"/>.</summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>The recipient's culture name, such as <c>ru</c>, for channels with per-language templates (WhatsApp).</summary>
    public string? Culture { get; init; }
}
