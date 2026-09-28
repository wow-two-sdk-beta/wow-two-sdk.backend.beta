using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

/// <summary>
/// Issues stateless purpose tokens (email confirmation, password reset, email change) and verifies them. A purpose may be
/// scoped as <c>{purpose}:{value}</c> to bind a token to that value as well. A token is an
/// expiry plus an HMAC-SHA256 over purpose, user id, security stamp and expiry, so rotating the stamp voids every token
/// issued before it. Nothing is stored.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
/// <typeparam name="TKey">The primary-key type.</typeparam>
public sealed class UserTokenIssuer<TUser, TKey>
    where TUser : IdentityUser<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private const byte FormatVersion = 1;
    private const int MacLength = 32;
    private const int PayloadLength = 1 + sizeof(long);

    private readonly byte[] _key;
    private readonly UserTokenOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Create the issuer over the configured key.</summary>
    /// <param name="options">Key and lifetimes.</param>
    /// <param name="timeProvider">The clock expiry is measured against.</param>
    public UserTokenIssuer(UserTokenOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options;
        _timeProvider = timeProvider;
        _key = Convert.FromBase64String(options.SigningKey ?? throw new InvalidOperationException("UserTokenOptions.SigningKey is required."));
    }

    /// <summary>Issue a token binding <paramref name="purpose"/> to <paramref name="user"/> and the current security stamp.</summary>
    /// <param name="user">The user the token acts for.</param>
    /// <param name="purpose">The operation the token authorizes.</param>
    public string Issue(TUser user, string purpose)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        var expires = (_timeProvider.GetUtcNow() + LifetimeOf(purpose)).ToUnixTimeSeconds();
        Span<byte> token = stackalloc byte[PayloadLength + MacLength];
        token[0] = FormatVersion;
        BinaryPrimitives.WriteInt64BigEndian(token[1..PayloadLength], expires);
        ComputeMac(user, purpose, expires, token[PayloadLength..]);
        return Base64Url.EncodeToString(token);
    }

    /// <summary>Whether <paramref name="token"/> is an unexpired token for this purpose, user and security stamp.</summary>
    /// <param name="user">The user the token claims to act for.</param>
    /// <param name="purpose">The operation being authorized.</param>
    /// <param name="token">The token the user presented.</param>
    public bool Verify(TUser user, string purpose, string? token)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        Span<byte> decoded = stackalloc byte[PayloadLength + MacLength];
        if (string.IsNullOrEmpty(token) || !TryDecode(token, decoded) || decoded[0] != FormatVersion)
            return false;

        var expires = BinaryPrimitives.ReadInt64BigEndian(decoded[1..PayloadLength]);
        if (expires <= _timeProvider.GetUtcNow().ToUnixTimeSeconds())
            return false;

        Span<byte> expected = stackalloc byte[MacLength];
        ComputeMac(user, purpose, expires, expected);
        return CryptographicOperations.FixedTimeEquals(expected, decoded[PayloadLength..]);
    }

    private static bool TryDecode(string token, Span<byte> destination)
    {
        try
        {
            return Base64Url.TryDecodeFromChars(token, destination, out var written) && written == destination.Length;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private TimeSpan LifetimeOf(string purpose)
    {
        var separator = purpose.IndexOf(UserTokenPurposeConstants.ScopeSeparator, StringComparison.Ordinal);
        var bare = separator < 0 ? purpose : purpose[..separator];
        return _options.Lifetimes.TryGetValue(bare, out var lifetime) ? lifetime : _options.DefaultLifetime;
    }

    private void ComputeMac(TUser user, string purpose, long expires, Span<byte> destination)
    {
        var material = string.Join(
            '\n',
            "wow2.identity.token.v1",
            purpose,
            Convert.ToString(user.Id, CultureInfo.InvariantCulture),
            user.SecurityStamp ?? string.Empty,
            expires.ToString(CultureInfo.InvariantCulture));
        HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(material), destination);
    }
}
