namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Represents one outgoing text message.</summary>
public sealed record SmsMessage
{
    /// <summary>Recipient in E.164 (<c>+998901234567</c>); brokers reformat it for their provider.</summary>
    public required string To { get; init; }

    /// <summary>The text.</summary>
    public required string Body { get; init; }

    /// <summary>Sender id or number; null uses <see cref="SmsOptions.DefaultFrom"/>.</summary>
    public string? From { get; init; }
}
