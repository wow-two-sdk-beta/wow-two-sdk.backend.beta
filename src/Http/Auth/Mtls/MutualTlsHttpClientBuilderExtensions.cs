using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.Mtls;

/// <summary>
/// Configures an outbound HTTP client to present a client certificate (mutual TLS).
/// </summary>
public static class MutualTlsHttpClientBuilderExtensions
{
    /// <summary>
    /// Presents the configured client certificate on every TLS handshake this client performs.
    /// Replaces the client's primary handler with a certificate-bearing <see cref="SocketsHttpHandler"/> —
    /// apply before resilience/hedging handlers if combining.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="configure">Certificate source (loaded instance or PKCS#12 path + password).</param>
    public static IHttpClientBuilder AddMutualTls(
        this IHttpClientBuilder builder,
        Action<MutualTlsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return builder.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var options = new MutualTlsOptions();
            configure(options);

            var certificate = options.ClientCertificate
                ?? (options.CertificatePath is not null
                    ? X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath, options.CertificatePassword)
                    : throw new InvalidOperationException("Mutual TLS requires ClientCertificate or CertificatePath to be configured."));

            var handler = new SocketsHttpHandler();
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { certificate };
            if (options.DangerousAcceptAnyServerCertificate)
            {
#pragma warning disable CA5359 // deliberate, explicitly named Dangerous* opt-in for dev/test loopback
                handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
            }

            return handler;
        });
    }

    /// <summary>Overload taking an already-loaded client certificate.</summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="clientCertificate">The certificate (with private key) to present.</param>
    public static IHttpClientBuilder AddMutualTls(this IHttpClientBuilder builder, X509Certificate2 clientCertificate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(clientCertificate);
        return builder.AddMutualTls(o => o.ClientCertificate = clientCertificate);
    }
}
