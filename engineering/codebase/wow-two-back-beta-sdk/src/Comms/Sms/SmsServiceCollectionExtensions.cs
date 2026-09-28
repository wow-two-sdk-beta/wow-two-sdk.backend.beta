using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms;

/// <summary>Cross-provider SMS defaults registration.</summary>
public static class SmsServiceCollectionExtensions
{
    /// <summary>Configures the default sender every <see cref="ISmsBroker"/> falls back to; pair with a provider registration.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Default sender.</param>
    public static IServiceCollection AddSmsDefaults(this IServiceCollection services, Action<SmsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddSmsOptions(configure);
    }

    internal static IServiceCollection AddSmsOptions(this IServiceCollection services, Action<SmsOptions>? configure)
        => services.AddValidatedOptions(
            configure,
            builder => builder.Validate(options => options.DefaultFrom is null || !string.IsNullOrWhiteSpace(options.DefaultFrom), "SmsOptions.DefaultFrom must not be blank when supplied."));

    /// <summary>The recipient without its leading <c>+</c>, as digit-only providers expect it.</summary>
    /// <param name="to">The E.164 recipient.</param>
    internal static string ToDigits(string to) => to.Trim().TrimStart('+');
}
