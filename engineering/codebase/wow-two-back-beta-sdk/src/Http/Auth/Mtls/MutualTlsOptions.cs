using System.Security.Cryptography.X509Certificates;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.Mtls;

/// <summary>Configuration for mutual-TLS HTTP clients; provide either <see cref="ClientCertificate"/> or a <see cref="CertificatePath"/> (PKCS#12 / .pfx).</summary>
public sealed class MutualTlsOptions
{
    /// <summary>Pre-loaded client certificate (takes precedence over <see cref="CertificatePath"/>).</summary>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>Path to a PKCS#12 (.pfx / .p12) file holding the client certificate + private key.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Password for the PKCS#12 file, if any.</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>Gets or sets whether to accept any server certificate (skips chain and name validation). Dev/test loopback only — never enable against real endpoints.</summary>
    public bool DangerousAcceptAnyServerCertificate { get; set; }
}
