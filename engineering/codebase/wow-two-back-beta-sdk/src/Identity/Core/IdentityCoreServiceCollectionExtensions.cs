using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>DI entry point for identity core — the mandatory slice every app starts from.</summary>
public static class IdentityCoreServiceCollectionExtensions
{
    /// <summary>Register identity core for a <see cref="Guid"/>-keyed user: normalizer, options, and the <see cref="UserAccountManager{TUser,TKey}"/> facade. Chain <c>.AddEntityFrameworkStores&lt;TContext&gt;()</c> and further slices.</summary>
    /// <typeparam name="TUser">The user entity (derives <see cref="IdentityUser"/>).</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional identity options.</param>
    public static IdentityBuilder<TUser, Guid> AddUserAccounts<TUser>(this IServiceCollection services, Action<IdentityCoreOptions>? configure = null)
        where TUser : IdentityUser<Guid>
        => services.AddUserAccounts<TUser, Guid>(configure);

    /// <summary>Register identity core for a <typeparamref name="TKey"/>-keyed user.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional identity options.</param>
    public static IdentityBuilder<TUser, TKey> AddUserAccounts<TUser, TKey>(this IServiceCollection services, Action<IdentityCoreOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new IdentityCoreOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();
        services.TryAddScoped<UserAccountManager<TUser, TKey>>();

        return new IdentityBuilder<TUser, TKey>(services);
    }
}
