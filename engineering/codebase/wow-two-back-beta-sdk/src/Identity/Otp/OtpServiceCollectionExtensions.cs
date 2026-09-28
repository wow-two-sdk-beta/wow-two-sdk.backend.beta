using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>OTP service registration.</summary>
public static class OtpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOtpService"/> with the code generator, the in-memory store, the message formatter and the
    /// delivery-handler factory. Options come from <paramref name="configure"/>, then the host section
    /// <see cref="OtpOptions.SectionName"/>. Register a shared <see cref="IOtpRepository"/> before this call for
    /// multi-instance hosts, and pair it with an <c>Add…OtpDelivery</c> channel.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Code length and kind, lifetime, rate limit, attempts and message templates.</param>
    public static IServiceCollection AddOtpService(
        this IServiceCollection services,
        Action<OtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            OtpOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.DefaultSpec.IsValid, "OtpOptions needs a CodeLength of 4–12, a known CodeKind and a positive CodeLifetime.")
                .Validate(o => o.MaxAttempts > 0, "OtpOptions.MaxAttempts must be positive.")
                .Validate(o => o.RateLimitWindow >= TimeSpan.Zero, "OtpOptions.RateLimitWindow must not be negative.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Messages.DefaultCulture), "OtpOptions.Messages.DefaultCulture must not be empty."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.TryAddSingleton<IOtpRepository, MemoryOtpRepository>();
        services.TryAddSingleton<IOtpMessageFormatter, OtpMessageFormatter>();
        services.TryAddScoped<IOtpDeliveryHandlerFactory, OtpDeliveryHandlerFactory>();
        services.TryAddScoped<IOtpService, OtpService>();

        return services;
    }

    /// <summary>Registers <typeparamref name="THandler"/> for <paramref name="channel"/>: keyed for methods and additive for callers that enumerate handlers.</summary>
    /// <typeparam name="THandler">The delivery handler.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="channel">The channel name the handler answers to.</param>
    internal static IServiceCollection AddOtpDeliveryHandler<THandler>(this IServiceCollection services, string channel)
        where THandler : class, IOtpDeliveryHandler
    {
        services.AddOtpService();
        services.TryAddScoped<THandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOtpDeliveryHandler, THandler>());
        services.TryAddKeyedScoped<IOtpDeliveryHandler>(channel, (provider, _) => provider.GetRequiredService<THandler>());
        return services;
    }
}
