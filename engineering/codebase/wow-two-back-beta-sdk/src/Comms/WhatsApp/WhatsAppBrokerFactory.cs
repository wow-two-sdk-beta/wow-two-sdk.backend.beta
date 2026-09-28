using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;

/// <summary>Creates WhatsApp brokers keyed by name; no name takes <see cref="WhatsAppOptions.DefaultBroker"/>, then the first registered.</summary>
/// <param name="services">The root provider; brokers are keyed singletons.</param>
/// <param name="options">The defaults; <c>Comms:WhatsApp:DefaultBroker</c> reloads live.</param>
public sealed class WhatsAppBrokerFactory(IServiceProvider services, IOptionsMonitor<WhatsAppOptions> options) : IWhatsAppBrokerFactory
{
    /// <inheritdoc />
    public IWhatsAppBroker? Create(string? name = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? options.CurrentValue.DefaultBroker : name;
        return string.IsNullOrWhiteSpace(name)
            ? services.GetService<IWhatsAppBroker>()
            : services.GetKeyedService<IWhatsAppBroker>(name.Trim().ToLowerInvariant());
    }
}
