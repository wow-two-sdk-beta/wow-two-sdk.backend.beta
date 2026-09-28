using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Creates OTP delivery handlers keyed by channel name, as each <c>Add…OtpDelivery</c> registered them.</summary>
/// <param name="services">The scope's provider; handlers are keyed services.</param>
public sealed class OtpDeliveryHandlerFactory(IServiceProvider services) : IOtpDeliveryHandlerFactory
{
    /// <inheritdoc />
    public IOtpDeliveryHandler? Create(string channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        return services.GetKeyedService<IOtpDeliveryHandler>(channel.ToLowerInvariant());
    }
}
