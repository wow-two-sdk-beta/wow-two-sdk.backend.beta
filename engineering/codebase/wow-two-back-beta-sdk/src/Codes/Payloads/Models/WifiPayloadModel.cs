namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Represents supplied data for WifiPayload encoding.</summary>
public sealed record WifiPayloadModel
{
    /// <summary>Gets the Ssid value.</summary>
    public string Ssid { get; init; } = string.Empty;

    /// <summary>Gets the Password value.</summary>
    public string? Password { get; init; }

    /// <summary>Gets the Authentication value.</summary>
    public WifiAuthentication Authentication { get; init; } = WifiAuthentication.Wpa;

    /// <summary>Gets the Hidden value.</summary>
    public bool Hidden { get; init; }
}
