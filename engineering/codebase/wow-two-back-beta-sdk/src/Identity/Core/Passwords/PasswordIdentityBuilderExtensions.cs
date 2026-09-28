using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;

/// <summary>Opts the password slice and its optional breach check into an identity registration.</summary>
public static class PasswordIdentityBuilderExtensions
{
    /// <summary>
    /// Register <see cref="UserPasswordService{TUser,TKey}"/> with the <see cref="PasswordOptions"/> rules and an Argon2id
    /// hasher. A hasher the host registered earlier wins.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    public static IdentityBuilder<TUser, TKey> AddArgon2Passwords<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IPasswordHasher<TUser>, Argon2PasswordHasher<TUser>>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserPasswordValidator<TUser>, PasswordRulesValidator<TUser, TKey>>());
        builder.Services.TryAddScoped<UserPasswordService<TUser, TKey>>();
        return builder;
    }

    /// <summary>Reject passwords found in the Pwned Passwords corpus, queried by k-anonymity range.</summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <param name="configure">Optional endpoint, threshold, timeout and fail-open settings.</param>
    public static IdentityBuilder<TUser, TKey> AddBreachedPasswordCheck<TUser, TKey>(
        this IdentityBuilder<TUser, TKey> builder,
        Action<BreachedPasswordOptions>? configure = null)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddValidatedOptions(
            configure,
            options => options
                .Validate(o => o.BaseAddress.IsAbsoluteUri, "BreachedPasswordOptions.BaseAddress must be absolute.")
                .Validate(o => o.MinimumBreachCount > 0, "BreachedPasswordOptions.MinimumBreachCount must be positive.")
                .Validate(o => o.Timeout > TimeSpan.Zero, "BreachedPasswordOptions.Timeout must be positive."));
        builder.Services.AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>((serviceProvider, http) =>
        {
            var options = serviceProvider.GetRequiredService<BreachedPasswordOptions>();
            http.BaseAddress = options.BaseAddress;
            http.Timeout = options.Timeout;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WoW2.Sdk.Backend.Beta");
        });
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserPasswordValidator<TUser>, BreachedPasswordValidator<TUser>>());
        return builder;
    }
}
