using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;

/// <summary>Registers config-activated conditional GET handling.</summary>
public static class ConditionalRequestServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="ConditionalRequestSettings"/> from the container's <see cref="IConfiguration"/> section
    /// <c>ConditionalRequests</c>, reloading with it; <c>AddApiDefaults</c> calls this already.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddConditionalRequests(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IOptionsChangeTokenSource<ConditionalRequestSettings>)))
            return services;

        services.AddOptions<ConditionalRequestSettings>()
            .Configure<IServiceProvider>((settings, provider) =>
                provider.GetService<IConfiguration>()?.GetSection(ConditionalRequestSettings.SectionName).Bind(settings));
        services.AddSingleton<IOptionsChangeTokenSource<ConditionalRequestSettings>>(provider =>
            new ConfigurationChangeTokenSource<ConditionalRequestSettings>(
                (IConfiguration?)provider.GetService<IConfiguration>()?.GetSection(ConditionalRequestSettings.SectionName)
                    ?? new ConfigurationBuilder().Build()));
        return services;
    }

    /// <summary>Adds <see cref="ConditionalRequestMiddleware"/>; place it inside response compression and before output caching.</summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseConditionalRequests(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ConditionalRequestMiddleware>();
    }
}
