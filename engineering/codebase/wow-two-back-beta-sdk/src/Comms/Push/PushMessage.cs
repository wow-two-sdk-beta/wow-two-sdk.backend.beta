namespace WoW.Two.Sdk.Backend.Beta.Comms.Push;

/// <summary>Represents one notification for one device.</summary>
public sealed record PushMessage
{
    /// <summary>The provider's device registration token.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Visible title; null with a null <see cref="Body"/> sends a silent (data-only) notification.</summary>
    public string? Title { get; init; }

    /// <summary>Visible body text.</summary>
    public string? Body { get; init; }

    /// <summary>Custom key/value data delivered to the app.</summary>
    public IReadOnlyDictionary<string, string>? Data { get; init; }

    /// <summary>App-icon badge count (APNs).</summary>
    public int? Badge { get; init; }

    /// <summary>Sound name; <c>default</c> plays the system sound.</summary>
    public string? Sound { get; init; }

    /// <summary>Notifications sharing this key replace each other on the device.</summary>
    public string? CollapseKey { get; init; }

    /// <summary>How long the provider keeps trying an offline device; null leaves the provider default.</summary>
    public TimeSpan? TimeToLive { get; init; }
}
