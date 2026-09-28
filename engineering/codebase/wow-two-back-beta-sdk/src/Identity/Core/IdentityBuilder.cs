using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Fluent builder returned by <c>AddUserAccounts</c>; each <c>.AddX()</c> opts a slice into the registration.</summary>
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

    /// <summary>Resolves the context the EF repositories use; set by <see cref="AddEntityFrameworkStores{TContext}"/>.</summary>
    internal Func<IServiceProvider, DbContext>? ContextFactory { get; private set; }

    /// <summary>Register the EF Core core store (<see cref="EfUserRepository{TUser,TKey,TContext}"/>) over <typeparamref name="TContext"/>.</summary>
    /// <typeparam name="TContext">The application DbContext hosting the identity schema (call <c>ApplyIdentitySchema</c> in its <c>OnModelCreating</c>).</typeparam>
    public IdentityBuilder<TUser, TKey> AddEntityFrameworkStores<TContext>()
        where TContext : DbContext
    {
        Services.TryAddScoped<IUserRepository<TUser, TKey>, EfUserRepository<TUser, TKey, TContext>>();
        ContextFactory = serviceProvider => serviceProvider.GetRequiredService<TContext>();
        return this;
    }

    /// <summary>
    /// Register the roles slice: <see cref="RoleService{TRole,TKey}"/>, <see cref="UserRoleService{TUser,TRole,TKey}"/> and,
    /// after <see cref="AddEntityFrameworkStores{TContext}"/>, their EF repositories. Principals then carry role claims.
    /// </summary>
    /// <typeparam name="TRole">The role entity mapped by <c>ApplyIdentitySchema</c>.</typeparam>
    public IdentityBuilder<TUser, TKey> AddRoles<TRole>()
        where TRole : IdentityRole<TKey>
    {
        if (ContextFactory is { } context)
        {
            Services.TryAddScoped<IRoleRepository<TRole, TKey>>(serviceProvider => new EfRoleRepository<TRole, TKey>(context(serviceProvider)));
            Services.TryAddScoped<IUserRoleRepository<TKey>>(serviceProvider => new EfUserRoleRepository<TRole, TKey>(context(serviceProvider)));
        }

        Services.TryAddScoped<RoleService<TRole, TKey>>();
        Services.TryAddScoped<UserRoleService<TUser, TRole, TKey>>();
        Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPersonalDataExporter<TUser>, RolePersonalDataExporter<TUser, TRole, TKey>>());
        return this;
    }
}
