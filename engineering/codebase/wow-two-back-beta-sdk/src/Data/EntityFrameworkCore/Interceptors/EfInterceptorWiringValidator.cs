using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>
/// Fails the host at boot when an interceptor registered via <c>AddEfInterceptor</c>/<c>AddEfSaveChangesInterceptor</c>
/// is not attached to an SDK-registered <see cref="DbContext"/>.
/// </summary>
/// <remarks>
///   - catches a silent failure: tenant stamping, auditing, outbox never run
///   - nothing logs it
///   - only contexts the SDK registered are checked, so a hand-rolled <c>AddDbContext</c> is never failed for opting out
/// </remarks>
internal sealed class EfInterceptorWiringValidator : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly EfContextWiringRegistry _registry;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="services">The root provider used to open a scope per context.</param>
    /// <param name="registry">The registry of SDK-registered DbContext types.</param>
    public EfInterceptorWiringValidator(IServiceProvider services, EfContextWiringRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);

        _services = services;
        _registry = registry;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var contextTypes = _registry.Snapshot();
        if (contextTypes.Length == 0)
            return Task.CompletedTask;

        using var scope = _services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var registered = serviceProvider.GetServices<IInterceptor>().Distinct().ToArray();
        if (registered.Length == 0)
            return Task.CompletedTask;

        foreach (var contextType in contextTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Resolving builds the options, which runs whichever callback is live and stamps the registry.
            var options = (DbContextOptions?)serviceProvider
                .GetService(typeof(DbContextOptions<>).MakeGenericType(contextType));

            if (options is null)
                continue;

            // Skip a context whose registration was replaced after the SDK added it.
            if (!_registry.WasConfiguredBySdk(contextType))
                continue;

            var attached = options.Attached();
            var missing = registered.Where(interceptor => !attached.Contains(interceptor)).ToArray();

            if (missing.Length > 0)
                throw new InvalidOperationException(BuildMessage(contextType, missing));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string BuildMessage(Type contextType, IInterceptor[] missing)
    {
        var names = string.Join(", ", missing.Select(interceptor => interceptor.GetType().Name));

        return $"EF interceptor wiring is broken for '{contextType.Name}': {missing.Length} interceptor(s) are "
             + $"registered in DI but never attached to the context — {names}. They would be resolvable and silently "
             + "never run. Route the context registration through "
             + $"{nameof(EfInterceptorExtensions)}.{nameof(EfInterceptorExtensions.AddRegisteredInterceptors)} "
             + "(AddEntityFrameworkCore<T> and AddPostgresPersistence<T> already do).";
    }
}
