using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>OTP service registration.</summary>
public static class OtpServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IOtpService"/> with the numeric code generator and in-memory store; register your own <see cref="IOtpStore"/> before this call for multi-instance, and pair with a delivery package or <see cref="IOtpDeliveryHandler"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional override of code length / lifetime / rate limit / attempts.</param>
    public static IServiceCollection AddOtpService(
        this IServiceCollection services,
        Action<OtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<OtpOptions>();
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOtpCodeGenerator, NumericOtpCodeGenerator>();
        services.TryAddSingleton<IOtpStore, MemoryOtpStore>();
        services.TryAddScoped<IOtpService, OtpService>();
        return services;
    }
}
