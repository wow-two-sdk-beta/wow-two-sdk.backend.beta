using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;

/// <summary>Opts sign-in orchestration into an identity registration.</summary>
public static class SignInIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="SignInService{TUser,TKey}"/>. It composes whichever of the password, lockout, token and
    /// two-factor slices are registered; preconditions come from <see cref="IdentityCoreOptions.SignIn"/>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddSignIn<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddScoped<SignInService<TUser, TKey>>();
        return builder;
    }
}
