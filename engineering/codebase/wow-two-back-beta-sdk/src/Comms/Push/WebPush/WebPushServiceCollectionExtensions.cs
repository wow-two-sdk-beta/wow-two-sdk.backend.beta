using System.Buffers.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.WebPush;

/// <summary>Web Push registration.</summary>
public static class WebPushServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IPushBroker"/> backed by the Web Push protocol with VAPID authentication.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">VAPID subject and key pair.</param>
    public static IHttpClientBuilder AddWebPushBroker(this IServiceCollection services, Action<WebPushOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            builder => builder
                .Validate(o => o.Subject.StartsWith("mailto:", StringComparison.Ordinal) || o.Subject.StartsWith("https://", StringComparison.Ordinal), "WebPushOptions.Subject must be a mailto: or https: URI.")
                .Validate(o => DecodedLength(o.PublicKey) == 65, "WebPushOptions.PublicKey must be a base64url uncompressed P-256 point (65 bytes).")
                .Validate(o => DecodedLength(o.PrivateKey) == 32, "WebPushOptions.PrivateKey must be a base64url P-256 scalar (32 bytes)."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPushBroker, WebPushBroker>();
        return services.AddHttpClient(WebPushBroker.HttpClientName);
    }

    private static int DecodedLength(string value)
    {
        try
        {
            return string.IsNullOrWhiteSpace(value) ? 0 : Base64Url.DecodeFromChars(value).Length;
        }
        catch (FormatException)
        {
            return 0;
        }
    }
}
