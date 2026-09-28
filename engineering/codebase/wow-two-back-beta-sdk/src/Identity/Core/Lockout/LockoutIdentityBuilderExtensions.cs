using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;

/// <summary>Opts the lockout slice into an identity registration.</summary>
public static class LockoutIdentityBuilderExtensions
{
    /// <summary>Register <see cref="UserLockoutService{TUser,TKey}"/> over <see cref="LockoutOptions"/>.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddLockout<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddScoped<UserLockoutService<TUser, TKey>>();
        return builder;
    }
}
