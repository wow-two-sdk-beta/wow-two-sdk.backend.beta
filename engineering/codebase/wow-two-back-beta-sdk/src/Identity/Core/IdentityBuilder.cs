using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Fluent builder returned by <c>AddIdentityCore</c>; each <c>.AddX()</c> opts a slice into the registration.</summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public sealed class IdentityBuilder<TUser, TKey>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Create the builder over <paramref name="services"/>.</summary>
    /// <param name="services">The service collection being configured.</param>
    public IdentityBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    /// <summary>The underlying service collection, for further composition.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Register the EF Core core store (<see cref="EfUserStore{TUser,TKey,TContext}"/>) over <typeparamref name="TContext"/>.</summary>
    /// <typeparam name="TContext">The application DbContext hosting the identity schema (call <c>ApplyIdentitySchema</c> in its <c>OnModelCreating</c>).</typeparam>
    public IdentityBuilder<TUser, TKey> AddEntityFrameworkStores<TContext>()
        where TContext : DbContext
    {
        Services.TryAddScoped<IUserStore<TUser, TKey>, EfUserStore<TUser, TKey, TContext>>();
        return this;
    }
}
