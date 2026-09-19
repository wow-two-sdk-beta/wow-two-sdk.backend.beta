using System.Security.Cryptography;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Identifiers.Generators;

/// <summary>Generates random identifiers using unbiased cryptographic character selection.</summary>
public sealed class CryptographicIdGenerator : IIdGenerator
{
    /// <summary>Generates an identifier of the requested length without guaranteeing uniqueness.</summary>
    /// <param name="length">Positive output length, at most 4096 characters.</param>
    /// <param name="alphabet">At least two distinct ASCII letters or digits, hyphen or underscore.</param>
    /// <remarks>The caller owns identifier length, collision retries and a unique storage constraint.</remarks>
    public string Generate(int length, string alphabet = IdAlphabetConstants.Alphanumeric)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 4096);
        ArgumentException.ThrowIfNullOrEmpty(alphabet);
        if (alphabet.Length < 2 || alphabet.Distinct().Count() != alphabet.Length
            || alphabet.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Use at least two distinct URL-safe ASCII characters.", nameof(alphabet));
        return string.Create(length, alphabet, static (destination, characters) =>
        {
            foreach (ref var character in destination)
                character = characters[RandomNumberGenerator.GetInt32(characters.Length)];
        });
    }
}
