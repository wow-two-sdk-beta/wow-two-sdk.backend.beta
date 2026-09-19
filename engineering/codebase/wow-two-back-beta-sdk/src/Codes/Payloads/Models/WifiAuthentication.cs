namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Identifies the scanner WIFI authentication token.</summary>
public enum WifiAuthentication
{
    /// <summary>WPA-family authentication.</summary>
    Wpa,
    /// <summary>Legacy WEP authentication.</summary>
    Wep,
    /// <summary>Open network without a password.</summary>
    None,
}
