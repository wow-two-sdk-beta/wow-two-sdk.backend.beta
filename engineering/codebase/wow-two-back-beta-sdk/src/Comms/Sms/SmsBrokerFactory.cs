using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Creates SMS brokers keyed by name; no name takes <see cref="SmsOptions.DefaultBroker"/>, then the first registered.</summary>
/// <param name="services">The root provider; brokers are keyed singletons.</param>
/// <param name="options">The defaults; <c>Comms:Sms:DefaultBroker</c> reloads live.</param>
public sealed class SmsBrokerFactory(IServiceProvider services, IOptionsMonitor<SmsOptions> options) : ISmsBrokerFactory
{
    /// <inheritdoc />
    public ISmsBroker? Create(string? name = null)
    {
        name = string.IsNullOrWhiteSpace(name) ? options.CurrentValue.DefaultBroker : name;
        return string.IsNullOrWhiteSpace(name)
            ? services.GetService<ISmsBroker>()
            : services.GetKeyedService<ISmsBroker>(name.Trim().ToLowerInvariant());
    }
}
