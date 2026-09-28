using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;

/// <summary>Opts per-user claims into an identity registration.</summary>
public static class UserClaimIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="UserClaimService{TUser,TKey}"/> and, after <c>AddEntityFrameworkStores</c>, its EF repository.
    /// Principals then carry the user's claims.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddUserClaims<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.ContextFactory is { } context)
            builder.Services.TryAddScoped<IUserClaimRepository<TKey>>(serviceProvider => new EfUserClaimRepository<TKey>(context(serviceProvider)));

        builder.Services.TryAddScoped<UserClaimService<TUser, TKey>>();
        return builder;
    }
}
