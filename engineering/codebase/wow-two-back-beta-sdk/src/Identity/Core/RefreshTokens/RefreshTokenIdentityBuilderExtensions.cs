using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

/// <summary>Opts rotating refresh tokens into an identity registration.</summary>
public static class RefreshTokenIdentityBuilderExtensions
{
    /// <summary>Register <see cref="RefreshTokenService{TUser,TKey}"/> and, after <c>AddEntityFrameworkStores</c>, its EF repository.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Optional lifetime.</param>
    public static IdentityBuilder<TUser, TKey> AddRefreshTokens<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder, Action<RefreshTokenOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddValidatedOptions(
            configure,
            options => options.Validate(o => o.Lifetime > TimeSpan.Zero, "RefreshTokenOptions.Lifetime must be positive."));
        if (builder.ContextFactory is { } context)
            builder.Services.TryAddScoped<IRefreshTokenRepository<TKey>>(serviceProvider => new EfRefreshTokenRepository<TKey>(context(serviceProvider)));

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<RefreshTokenService<TUser, TKey>>();
        return builder;
    }
}
