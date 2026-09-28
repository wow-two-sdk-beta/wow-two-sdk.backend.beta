using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

namespace WoW.Two.Sdk.Backend.Beta.Web.Idempotency;

/// <summary>Registers config-activated HTTP idempotency.</summary>
public static class HttpIdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="HttpIdempotencySettings"/> live from the container's <c>HttpIdempotency</c> section and registers the
    /// in-memory <see cref="IIdempotencyRepository"/> unless another is registered (use <c>AddSqlIdempotencyRepository</c>
    /// for several hosts). <c>AddApiDefaults</c> calls this already.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddHttpIdempotency(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IOptionsChangeTokenSource<HttpIdempotencySettings>)))
            return services;

        services.AddOptions<HttpIdempotencySettings>()
            .Configure<IServiceProvider>((settings, provider) =>
                provider.GetService<IConfiguration>()?.GetSection(HttpIdempotencySettings.SectionName).Bind(settings));
        services.AddSingleton<IOptionsChangeTokenSource<HttpIdempotencySettings>>(provider =>
            new ConfigurationChangeTokenSource<HttpIdempotencySettings>(
                (IConfiguration?)provider.GetService<IConfiguration>()?.GetSection(HttpIdempotencySettings.SectionName)
                    ?? new ConfigurationBuilder().Build()));
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        return services;
    }

    /// <summary>Adds <see cref="HttpIdempotencyMiddleware"/>; place it after authentication so keys are per caller.</summary>
    /// <param name="app">The application builder.</param>
    public static IApplicationBuilder UseHttpIdempotency(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<HttpIdempotencyMiddleware>();
    }
}
