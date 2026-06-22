using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.Mtls;

/// <summary>Provides client-certificate (mutual TLS) configuration for an outbound HTTP client.</summary>
public static class MutualTlsHttpClientBuilderExtensions
{
    /// <summary>Presents the configured client certificate on every TLS handshake, replacing the primary handler; apply before resilience/hedging handlers if combining.</summary>
    /// <param name="builder">The HTTP client builder to extend.</param>
    /// <param name="configure">Configures the client certificate source and TLS behavior.</param>
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

    /// <summary>Overload taking an already-loaded client certificate (with private key) to present.</summary>
    /// <param name="builder">The HTTP client builder to extend.</param>
    /// <param name="clientCertificate">The pre-loaded client certificate, including its private key.</param>
    public static IHttpClientBuilder AddMutualTls(this IHttpClientBuilder builder, X509Certificate2 clientCertificate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(clientCertificate);
        return builder.AddMutualTls(o => o.ClientCertificate = clientCertificate);
    }
}
