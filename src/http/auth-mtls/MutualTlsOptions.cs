using System.Security.Cryptography.X509Certificates;

namespace WoW.Two.Sdk.Backend.Beta.http.auth_mtls;

/// <summary>
/// Client-certificate settings for mutual-TLS HTTP clients. Provide either a loaded
/// <see cref="ClientCertificate"/> or a <see cref="CertificatePath"/> (PKCS#12 / .pfx) to load from disk.
/// </summary>
public sealed class MutualTlsOptions
{
    /// <summary>Pre-loaded client certificate (takes precedence over <see cref="CertificatePath"/>).</summary>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>Path to a PKCS#12 (.pfx / .p12) file holding the client certificate + private key.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Password for the PKCS#12 file, if any.</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Accept any server certificate (skips chain + name validation). Dev/test loopback only —
    /// never enable against real endpoints.
    /// </summary>
    public bool DangerousAcceptAnyServerCertificate { get; set; }
}
