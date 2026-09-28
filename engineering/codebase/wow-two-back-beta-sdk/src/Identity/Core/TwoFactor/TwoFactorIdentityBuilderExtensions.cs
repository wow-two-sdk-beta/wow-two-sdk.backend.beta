using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;

/// <summary>Opts the two-factor slice into an identity registration.</summary>
public static class TwoFactorIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="UserTwoFactorService{TUser,TKey}"/> (authenticator + recovery codes) and
    /// <see cref="UserTwoFactorMethodService{TUser,TKey}"/> (delivered codes) with the TOTP and OTP services and, after
    /// <c>AddEntityFrameworkStores</c>, the stored-token repository. Two-factor sign-in also needs <c>.AddUserTokens(...)</c>;
    /// each delivered method needs its channel registered (<c>AddSmsOtpDelivery</c>, <c>AddWhatsAppOtpDelivery</c>, …).
    /// Options come from <paramref name="configure"/>, then the host section <c>Identity:TwoFactor</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Authenticator label, recovery-code count and delivered-code methods.</param>
    public static IdentityBuilder<TUser, TKey> AddTwoFactor<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder, Action<TwoFactorOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddModuleOptions(
            TwoFactorOptions.SectionName,
            configure,
            options => options
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "TwoFactorOptions.Issuer is required.")
                .Validate(o => o.RecoveryCodeCount is > 0 and <= 100, "TwoFactorOptions.RecoveryCodeCount must lie between 1 and 100.")
                .Validate(o => o.Methods.Values.All(m => !string.IsNullOrWhiteSpace(m.Channel)), "Every TwoFactorOptions.Methods entry needs a Channel.")
                .Validate(o => o.Methods.Values.All(m => m.Code.IsValid), "Every TwoFactorOptions.Methods code needs a Length of 4–12, a known Kind and a positive Lifetime.")
                .Validate(o => !o.Methods.ContainsKey(TwoFactorMethodNameConstants.Authenticator), "'authenticator' is reserved; name delivered methods otherwise."));
        builder.Services.AddTotp();
        builder.Services.AddOtpService();
        builder.AddStoredUserTokens();
        builder.Services.TryAddScoped<UserTwoFactorService<TUser, TKey>>();
        builder.Services.TryAddScoped<UserTwoFactorMethodService<TUser, TKey>>();
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
