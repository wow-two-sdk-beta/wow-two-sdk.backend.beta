namespace WoW.Two.Sdk.Backend.Beta.Web.DataProtection;

/// <summary>Holds options for a Data Protection key ring that survives restarts and container replacement.</summary>
public sealed record PersistentDataProtectionOptions
{
    /// <summary>Gets or sets the application name that isolates this product's keys and payloads from other apps.</summary>
    /// <remarks>Every host that must read another host's cookies or tokens uses the same name and key directory.</remarks>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory that holds the key ring, such as a mounted volume; required outside Development.</summary>
    public string? KeyDirectory { get; set; }
}
