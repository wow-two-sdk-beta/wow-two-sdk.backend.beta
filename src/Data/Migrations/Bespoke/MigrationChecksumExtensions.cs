using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides the stable content-checksum extension used to detect drift in applied migrations.</summary>
public static class MigrationChecksumExtensions
{
    /// <summary>Computes the lowercase-hex SHA-256 of the normalized content (CR/CRLF collapsed to LF, trailing whitespace trimmed).</summary>
    /// <param name="content">The migration SQL body to hash.</param>
    public static string ToMigrationChecksum(this string content)
    {
        // Normalize line endings and trailing whitespace so cross-machine churn never reads as drift.
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
