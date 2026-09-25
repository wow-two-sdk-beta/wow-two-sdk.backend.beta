using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions.BackgroundServices;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Provides opt-in transaction coordination for one primary EF context.</summary>
public static class DataSessionServiceCollectionExtensions
{
    /// <summary>Registers a scoped session without changing context, factory or repository registrations.</summary>
    /// <typeparam name="TContext">The already registered relational context.</typeparam>
    /// <param name="services">The collection receiving the session.</param>
    /// <param name="configure">Optional depth and cleanup budgets.</param>
    public static IServiceCollection AddDataSession<TContext>(
        this IServiceCollection services,
        Action<DataSessionOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDataSession)))
        {
            throw new InvalidOperationException("Register exactly one primary data session per service provider.");
        }
        var options = new DataSessionOptions();
        configure?.Invoke(options);
        if (options.MaxDepth is < 1 or > 32 || options.HookTimeout <= TimeSpan.Zero
            || options.RollbackTimeout <= TimeSpan.Zero
            || options.HookTimeout > TimeSpan.FromMinutes(5) || options.RollbackTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(configure), "Use depth 1..32 and cleanup budgets in (0, 5 minutes].");
        }
        services.AddSingleton(options with { });
        services.AddLogging();
        services.TryAddSingleton<DataSessionScopeValidator>();
        services.AddScoped<IDataSession>(provider =>
        {
            provider.GetRequiredService<DataSessionScopeValidator>().Validate(provider);
            return ActivatorUtilities.CreateInstance<EfDataSession<TContext>>(provider);
        });
        services.AddHostedService<DataSessionValidationBackgroundService>();
        return services;
    }
}
