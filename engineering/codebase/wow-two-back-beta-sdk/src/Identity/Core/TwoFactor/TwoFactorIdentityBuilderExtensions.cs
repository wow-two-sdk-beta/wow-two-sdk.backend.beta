using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Opts the two-factor slice into an identity registration.</summary>
public static class TwoFactorIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="UserTwoFactorService{TUser,TKey}"/> with the TOTP service and, after
    /// <c>AddEntityFrameworkStores</c>, the stored-token repository. Two-factor sign-in also needs <c>.AddUserTokens(...)</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Optional authenticator label and recovery-code count.</param>
    public static IdentityBuilder<TUser, TKey> AddTwoFactor<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder, Action<TwoFactorOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddValidatedOptions(
            configure,
            options => options
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "TwoFactorOptions.Issuer is required.")
                .Validate(o => o.RecoveryCodeCount is > 0 and <= 100, "TwoFactorOptions.RecoveryCodeCount must lie between 1 and 100."));
        builder.Services.AddTotp();
        builder.AddStoredUserTokens();
        builder.Services.TryAddScoped<UserTwoFactorService<TUser, TKey>>();
        return builder;
    }

    /// <summary>Register the EF stored-token repository once the context is known.</summary>
    internal static void AddStoredUserTokens<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        if (builder.ContextFactory is { } context)
            builder.Services.TryAddScoped<IUserTokenRepository<TKey>>(serviceProvider => new EfUserTokenRepository<TKey>(context(serviceProvider)));
    }
}
