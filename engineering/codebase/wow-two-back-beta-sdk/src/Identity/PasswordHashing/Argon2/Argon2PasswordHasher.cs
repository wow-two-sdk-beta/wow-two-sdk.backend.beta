using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.PasswordHashing.Argon2;

/// <summary>Argon2id password hasher (OWASP-recommended); implements <see cref="IPasswordHasher{TUser}"/> so it slots into ASP.NET Core Identity.</summary>
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
    /// <remarks>
    /// Verifies with the parameters recorded in the hash, so raising the cost never locks out existing users; a match
    /// recorded with other parameters returns <see cref="PasswordVerificationResult.SuccessRehashNeeded"/>.
    /// </remarks>
    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        var parts = hashedPassword.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5 || parts[0] != "argon2id" || !TryReadCost(parts[2], out var memoryKb, out var iterations, out var parallelism))
            return PasswordVerificationResult.Failed;

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            if (expected.Length is < 16 or > 64)
                return PasswordVerificationResult.Failed;

            var actual = HashCore(providedPassword, salt, memoryKb, iterations, parallelism, expected.Length);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                return PasswordVerificationResult.Failed;

            return memoryKb == MemoryKb && iterations == Iterations && parallelism == Parallelism && expected.Length == HashSize
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static byte[] HashCore(string password, byte[] salt)
        => HashCore(password, salt, MemoryKb, Iterations, Parallelism, HashSize);

    private static byte[] HashCore(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int hashSize)
    {
        using var argon = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            MemorySize = memoryKb,
            Iterations = iterations,
        };
        return argon.GetBytes(hashSize);
    }

    /// <summary>Reads <c>m=…,t=…,p=…</c>, bounding each value so a stored hash cannot demand unbounded work.</summary>
    private static bool TryReadCost(string segment, out int memoryKb, out int iterations, out int parallelism)
    {
        memoryKb = iterations = parallelism = 0;
        foreach (var pair in segment.Split(','))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0
                || !int.TryParse(pair.AsSpan(separator + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
                return false;

            switch (pair[..separator])
            {
                case "m" when value is > 0 and <= 1_048_576: memoryKb = value; break;
                case "t" when value is > 0 and <= 64: iterations = value; break;
                case "p" when value is > 0 and <= 64: parallelism = value; break;
                default: return false;
            }
        }

        return memoryKb > 0 && iterations > 0 && parallelism > 0;
    }
}
