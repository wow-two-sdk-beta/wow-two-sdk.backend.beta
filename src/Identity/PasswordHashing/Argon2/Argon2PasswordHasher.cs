using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2;

/// <summary>
/// Argon2id password hasher (OWASP-recommended for new applications).
/// Compatible with <see cref="IPasswordHasher{TUser}"/> so it slots into ASP.NET Core Identity.
/// </summary>
/// <typeparam name="TUser">User type (any class).</typeparam>
public sealed class Argon2PasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 4;        // OWASP 2024 baseline
    private const int MemoryKb = 19_456;     // 19 MiB
    private const int Parallelism = 1;

    /// <inheritdoc />
    public string HashPassword(TUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashCore(password, salt);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        var parts = hashedPassword.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts[0] != "argon2id")
            return PasswordVerificationResult.Failed;

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            var actual = HashCore(providedPassword, salt);
            return CryptographicOperations.FixedTimeEquals(expected, actual)
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static byte[] HashCore(string password, byte[] salt)
    {
        using var argon = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemoryKb,
            Iterations = Iterations,
        };
        return argon.GetBytes(HashSize);
    }
}

/// <summary>Registration helpers.</summary>
public static class Argon2ServiceCollectionExtensions
{
    /// <summary>
    /// Replace the default `IPasswordHasher&lt;TUser&gt;` with Argon2id. Must run AFTER `AddIdentityCore` / `AddDefaultIdentity`.
    /// </summary>
    public static IServiceCollection UseArgon2PasswordHasher<TUser>(this IServiceCollection services)
        where TUser : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IPasswordHasher<TUser>>();
        services.AddSingleton<IPasswordHasher<TUser>, Argon2PasswordHasher<TUser>>();
        return services;
    }
}
