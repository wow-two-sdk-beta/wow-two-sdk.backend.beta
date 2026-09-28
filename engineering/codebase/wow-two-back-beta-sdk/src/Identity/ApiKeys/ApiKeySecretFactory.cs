using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Creates API key secrets and maps presented ones to what a store keeps — the hash and the prefix.</summary>
/// <param name="options">The key shape.</param>
public sealed class ApiKeySecretFactory(IOptions<ApiKeyOptions> options)
{
    /// <summary>Holds the secret alphabet as search values, for the shape check.</summary>
    private static readonly SearchValues<char> AlphabetValues = SearchValues.Create(ApiKeyOptions.Alphabet);

    /// <summary>Gets the key shape.</summary>
    private ApiKeyOptions Shape => options.Value;

    /// <summary>Creates a secret from the cryptographic random generator.</summary>
    /// <returns>The secret with its prefix and hash; store the hash and prefix, show the secret once.</returns>
    public ApiKeySecret Create()
    {
        var secret = Shape.Marker
            + new string(RandomNumberGenerator.GetItems<char>(ApiKeyOptions.Alphabet, Shape.RandomLength));
        return new ApiKeySecret { Secret = secret, Prefix = ToPrefix(secret), Hash = ToHash(secret) };
    }

    /// <summary>Maps a secret to its SHA-256; secrets are long and random, so a fast hash resists guessing.</summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The lowercase hex hash.</returns>
    public static string ToHash(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    /// <summary>Maps a secret to the prefix shown beside its key's name.</summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The marker and the first visible random characters.</returns>
    public string ToPrefix(string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return secret[..Math.Min(secret.Length, Shape.Marker.Length + Shape.VisibleLength)];
    }

    /// <summary>Checks whether text carries this product's marker — a key presented, valid or not.</summary>
    /// <param name="candidate">The presented text.</param>
    /// <returns><c>true</c> when the text starts with the marker.</returns>
    public bool HasMarker(string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.StartsWith(Shape.Marker, StringComparison.Ordinal);
    }

    /// <summary>Checks whether text has a secret's shape before any lookup.</summary>
    /// <param name="candidate">The presented text.</param>
    /// <returns><c>true</c> for the marker followed by the configured number of letters and digits.</returns>
    public bool IsSecretShaped(string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Length == Shape.Marker.Length + Shape.RandomLength
            && HasMarker(candidate)
            && !candidate.AsSpan(Shape.Marker.Length).ContainsAnyExcept(AlphabetValues);
    }
}
