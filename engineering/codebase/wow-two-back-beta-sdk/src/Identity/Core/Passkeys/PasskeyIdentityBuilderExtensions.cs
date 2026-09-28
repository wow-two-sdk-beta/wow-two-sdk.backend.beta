using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;

/// <summary>Opts the passkey slice into an identity registration.</summary>
public static class PasskeyIdentityBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="UserPasskeyService{TUser,TKey}"/> and, after <c>AddEntityFrameworkStores</c>, the passkey
    /// repository over <c>identity_passkeys</c>. Options come from <paramref name="configure"/>, then the host section
    /// <c>Identity:Passkeys</c>; the relying-party domain and at least one origin are required.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Relying party, origins and ceremony settings.</param>
    public static IdentityBuilder<TUser, TKey> AddPasskeys<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder, Action<PasskeyOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddModuleOptions(
            PasskeyOptions.SectionName,
            configure,
            options => options
                .Validate(o => !string.IsNullOrWhiteSpace(o.ServerDomain), "PasskeyOptions.ServerDomain is required.")
                .Validate(o => o.Origins.Count > 0, "PasskeyOptions.Origins needs at least one origin.")
                .Validate(o => o.CeremonyLifetime > TimeSpan.Zero, "PasskeyOptions.CeremonyLifetime must be positive."));
        builder.Services.AddDataProtection();
        if (builder.ContextFactory is { } context)
            builder.Services.TryAddScoped<IUserPasskeyRepository<TKey>>(serviceProvider => new EfUserPasskeyRepository<TKey>(context(serviceProvider)));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped<UserPasskeyService<TUser, TKey>>();
        return builder;
    }
}
