using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>
/// The single place a DI-registered EF Core interceptor is attached to a <see cref="DbContext"/>.
/// Every SDK registration path — <c>AddEntityFrameworkCore&lt;T&gt;</c> and <c>AddPostgresPersistence&lt;T&gt;</c> —
/// routes through <see cref="AddRegisteredInterceptors"/>; a bespoke registration should call it too.
/// </summary>
/// <remarks>
/// <para><strong>Ordering.</strong> Interceptors attach in DI registration order, and the whole DI set is attached
/// <em>before</em> the caller's provider configurator runs. So an interceptor registered first via
/// <c>AddEfInterceptor</c> runs first, and anything a caller attaches by hand inside its
/// <c>configureProvider</c> callback runs after the entire DI set. A guard interceptor that must see the change
/// tracker before audit/soft-delete/tenant stamping only has to be registered before them.</para>
/// <para><strong>Idempotence.</strong> Attachment is de-duplicated by reference, so calling this twice, or calling it
/// alongside <c>UseAuditInterceptor</c>/<c>UseSoftDeleteInterceptor</c>, cannot double-stamp.</para>
/// </remarks>
public static class EfInterceptorWiring
{
    /// <summary>Attaches every <see cref="IInterceptor"/> registered via <c>AddEfInterceptor</c>/<c>AddEfSaveChangesInterceptor</c> that is not already attached to this builder.</summary>
    /// <param name="builder">The DbContext options builder being configured.</param>
    /// <param name="serviceProvider">The provider the interceptors are resolved from.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static DbContextOptionsBuilder AddRegisteredInterceptors(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var attached = builder.Options.Attached();

        var pending = serviceProvider
            .GetServices<IInterceptor>()
            .Distinct()
            .Where(interceptor => !attached.Contains(interceptor))
            .ToArray();

        return pending.Length == 0 ? builder : builder.AddInterceptors(pending);
    }

    /// <summary>Reports whether the exact <paramref name="interceptor"/> instance is already attached to <paramref name="builder"/>.</summary>
    /// <param name="builder">The DbContext options builder to inspect.</param>
    /// <param name="interceptor">The interceptor instance to look for.</param>
    /// <returns><see langword="true"/> when the instance is already attached.</returns>
    public static bool HasInterceptor(this DbContextOptionsBuilder builder, IInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(interceptor);

        return builder.Options.Attached().Contains(interceptor);
    }

    /// <summary>Returns the interceptors currently attached to <paramref name="options"/>, never <see langword="null"/>.</summary>
    /// <param name="options">The built DbContext options to inspect.</param>
    /// <returns>The attached interceptors.</returns>
    internal static IReadOnlyList<IInterceptor> Attached(this DbContextOptions options)
        => options.FindExtension<CoreOptionsExtension>()?.Interceptors?.ToArray() ?? [];

    /// <summary>
    /// Applies the one SDK context-configuration path: DI interceptors first (so registration order is the whole
    /// order), then the caller's provider configurator, then the SDK's logging/tracking defaults.
    /// </summary>
    /// <param name="serviceProvider">The provider the context is being configured from.</param>
    /// <param name="builder">The DbContext options builder being configured.</param>
    /// <param name="options">The SDK EF Core defaults to apply.</param>
    /// <param name="configureProvider">The caller's database-provider configurator.</param>
    /// <param name="registry">The wiring registry the boot guard reads.</param>
    /// <param name="contextType">The concrete DbContext type being configured.</param>
    internal static void ApplySdkContextConfiguration(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder builder,
        EntityFrameworkCoreOptions options,
        Action<IServiceProvider, DbContextOptionsBuilder> configureProvider,
        EfContextWiringRegistry registry,
        Type contextType)
    {
        // Tells the boot guard this context's live options are the SDK's, not a replacement registration's.
        registry.MarkConfiguredBySdk(contextType);

        builder.AddRegisteredInterceptors(serviceProvider);

        configureProvider(serviceProvider, builder);

        var isDevelopment = serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;

        if (options.EnableSensitiveDataLogging ?? isDevelopment)
            builder.EnableSensitiveDataLogging();

        if (options.EnableDetailedErrors ?? isDevelopment)
            builder.EnableDetailedErrors();

        if (options.NoTrackingByDefault)
            builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}
