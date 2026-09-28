namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Represents a worded one-time-code message.</summary>
public sealed record OtpMessageModel
{
    /// <summary>The message text; carries the code.</summary>
    public required string Text { get; init; }

    /// <summary>The subject line, for channels that carry one.</summary>
    public required string Subject { get; init; }
}
