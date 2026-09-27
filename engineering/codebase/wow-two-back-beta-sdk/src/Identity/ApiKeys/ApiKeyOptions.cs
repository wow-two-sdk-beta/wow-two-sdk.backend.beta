namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Holds the shape of a product's API key secrets and how the scheme reads them.</summary>
public sealed class ApiKeyOptions
{
    /// <summary>Holds the letters and digits a secret draws from — URL- and header-safe.</summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Gets or sets the marker every secret starts with, so a leaked key is recognizable — such as <c>tf_</c>.</summary>
    public string Marker { get; set; } = "key_";

    /// <summary>Gets or sets how many random characters follow the marker; default 32 (about 190 bits).</summary>
    public int RandomLength { get; set; } = 32;

    /// <summary>Gets or sets how many random characters the display prefix shows after the marker; default 8.</summary>
    public int VisibleLength { get; set; } = 8;

    /// <summary>Gets or sets how stale a key's last-use instant may grow before the store is asked to write it again.</summary>
    public TimeSpan TouchInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the header a client may send the secret in; a Bearer token always works too.</summary>
    public string HeaderName { get; set; } = ApiKeyAuthenticationDefaults.HeaderName;
}
