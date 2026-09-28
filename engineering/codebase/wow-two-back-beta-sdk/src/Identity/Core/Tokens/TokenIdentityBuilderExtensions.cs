using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>Opts stateless purpose tokens into an identity registration.</summary>
public static class TokenIdentityBuilderExtensions
{
    /// <summary>Register <see cref="UserTokenIssuer{TUser,TKey}"/>; password reset and the email slice issue through it.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Sets the signing key and optional lifetimes.</param>
    public static IdentityBuilder<TUser, TKey> AddUserTokens<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder, Action<UserTokenOptions> configure)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddValidatedOptions(
            configure,
            options => options
                .Validate(o => DecodedLength(o.SigningKey) >= 32, "UserTokenOptions.SigningKey must be base64 of at least 32 bytes.")
                .Validate(o => o.DefaultLifetime > TimeSpan.Zero, "UserTokenOptions.DefaultLifetime must be positive.")
                .Validate(o => o.Lifetimes.Values.All(lifetime => lifetime > TimeSpan.Zero), "UserTokenOptions.Lifetimes must be positive."));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<UserTokenIssuer<TUser, TKey>>();
        return builder;
    }

    private static int DecodedLength(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return 0;

        var buffer = new byte[key.Length];
        return Convert.TryFromBase64String(key, buffer, out var written) ? written : 0;
    }
}
