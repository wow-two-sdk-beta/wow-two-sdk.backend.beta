namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Holds tunable inputs for envelope cryptography (appsettings section <c>EnvelopeCryptography</c>).</summary>
/// <remarks>Used by the default <see cref="EnvironmentMasterKeyBroker"/>; a custom <see cref="IMasterKeyBroker"/> may ignore <see cref="MasterKeyEnvironmentVariable"/> and read its own source.</remarks>
public sealed record EnvelopeCryptographyOptions
{
    /// <summary>The environment variable holding the base64-encoded master key. Default <c>MASTER_KEY</c>.</summary>
    public string MasterKeyEnvironmentVariable { get; set; } = "MASTER_KEY";
}
