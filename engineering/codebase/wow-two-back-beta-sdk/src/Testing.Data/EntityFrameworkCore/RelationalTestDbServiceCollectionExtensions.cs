using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>Registers a <see cref="RelationalTestDb{TContext}"/>-backed context through the SDK's own DI registration, so a test exercises the registration pipeline instead of bypassing it.</summary>
/// <remarks><see cref="RelationalTestDb{TContext}.NewContext"/> builds <see cref="DbContextOptions"/> directly — it never runs the interceptor auto-wire loop nor the pooling branch inside <c>AddEntityFrameworkCore</c>, so defects living there are invisible to it. Anything asserting <em>which interceptors are on the resolved options</em>, or pooled-context behaviour, must resolve the context from DI via this seam.</remarks>
public static class RelationalTestDbServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="TContext"/> via <c>AddEntityFrameworkCore</c>, pointed at the test database.</summary>
    /// <typeparam name="TContext">The application context under test.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="testDb">The started test database the context binds to.</param>
    /// <param name="configureOptions">An optional hook overriding the SDK defaults — most usefully <c>UsePooling</c>, which selects <c>AddDbContextPool</c> over <c>AddDbContext</c>.</param>
    public static IServiceCollection AddTestEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        RelationalTestDb<TContext> testDb,
        Action<EntityFrameworkCoreOptions>? configureOptions = null)
        where TContext : AppDbContextBase
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(testDb);

        return services.AddEntityFrameworkCore<TContext>(
            options => configureOptions?.Invoke(options),
            testDb.ApplyProvider);
    }
}
