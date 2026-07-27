using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>Records the DbContext types the SDK registered, so their interceptor wiring can be verified at boot.</summary>
internal sealed class EfContextWiringRegistry
{
    private readonly HashSet<Type> _contextTypes = [];
    private readonly HashSet<Type> _configuredBySdk = [];

    /// <summary>Records a DbContext type registered through an SDK registration path.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    public void Register(Type contextType)
    {
        lock (_contextTypes)
            _contextTypes.Add(contextType);
    }

    /// <summary>Records that the SDK's own options callback is the one that built this context's options.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    public void MarkConfiguredBySdk(Type contextType)
    {
        lock (_contextTypes)
            _configuredBySdk.Add(contextType);
    }

    /// <summary>Reports whether the SDK's options callback built this context's live options.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    /// <returns><see langword="false"/> when the registration was replaced after the SDK added it.</returns>
    public bool WasConfiguredBySdk(Type contextType)
    {
        lock (_contextTypes)
            return _configuredBySdk.Contains(contextType);
    }

    /// <summary>Returns a snapshot of the recorded DbContext types.</summary>
    /// <returns>The recorded context types.</returns>
    public Type[] Snapshot()
    {
        lock (_contextTypes)
            return [.. _contextTypes];
    }
}

/// <summary>
/// Fails the host at boot when an interceptor registered via <c>AddEfInterceptor</c>/<c>AddEfSaveChangesInterceptor</c>
/// is not attached to an SDK-registered <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// This is the guard for the defect class the wiring unification fixes: a registration path that resolves an
/// interceptor from DI but never hands it to the context. That failure is otherwise completely silent — the concern
/// (tenant stamping, auditing, outbox) simply never runs and nothing logs it. Only contexts the SDK itself registered
/// are checked, so a consumer's hand-rolled <c>AddDbContext</c> is never failed for opting out.
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

            // The SDK registration was replaced after the fact — a test host repointing the context onto another
            // provider, say. That context is no longer the SDK's to police.
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
             + $"{nameof(EfInterceptorWiring)}.{nameof(EfInterceptorWiring.AddRegisteredInterceptors)} "
             + "(AddEntityFrameworkCore<T> and AddPostgresPersistence<T> already do).";
    }
}

/// <summary>Wires the boot-time interceptor-attachment guard into a service collection.</summary>
internal static class EfInterceptorWiringValidatorServiceCollectionExtensions
{
    /// <summary>Returns the shared <see cref="EfContextWiringRegistry"/>, registering it and the boot guard on first use.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The registry every SDK context registration records itself in.</returns>
    public static EfContextWiringRegistry GetOrAddEfContextWiringRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(EfContextWiringRegistry)
                && descriptor.ImplementationInstance is EfContextWiringRegistry existing)
                return existing;

        var registry = new EfContextWiringRegistry();
        services.AddSingleton(registry);

        // Index 0 so the guard runs before migration/consumer hosted services touch the database.
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, EfInterceptorWiringValidator>());

        return registry;
    }
}
