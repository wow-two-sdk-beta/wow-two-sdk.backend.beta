using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Storage.Transfer;

/// <summary>Maps a transfer request — method, path, expiry and its one extra (file name or media type) — to its HMAC-SHA256 signature.</summary>
internal static class BlobTransferSignatureMapper
{
    /// <summary>The base64url signature of the request.</summary>
    public static string Sign(string key, string method, string path, long expires, string? extra)
        => Base64Url.EncodeToString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(Canonical(method, path, expires, extra))));

    /// <summary>Whether <paramref name="signature"/> is the request's signature, compared in constant time.</summary>
    public static bool Matches(string key, string method, string path, long expires, string? extra, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Sign(key, method, path, expires, extra));
        var presented = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    private static string Canonical(string method, string path, long expires, string? extra)
        => string.Join('\n', method, path, expires.ToString(CultureInfo.InvariantCulture), extra ?? string.Empty);
}
