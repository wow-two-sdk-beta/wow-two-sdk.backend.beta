using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2;

/// <summary>Registration helpers.</summary>
public static class Argon2ServiceCollectionExtensions
{
    /// <summary>Replaces the default <c>IPasswordHasher&lt;TUser&gt;</c> with Argon2id; must run after <c>AddIdentityCore</c> / <c>AddDefaultIdentity</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection UseArgon2PasswordHasher<TUser>(this IServiceCollection services)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IPasswordHasher<TUser>>();
        services.AddSingleton<IPasswordHasher<TUser>, Argon2PasswordHasher<TUser>>();
        return services;
    }
}
