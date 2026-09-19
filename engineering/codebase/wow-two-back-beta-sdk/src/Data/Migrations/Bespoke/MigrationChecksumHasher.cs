using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Hashes a migration body with SHA-256 over its normalized text — CR/CRLF collapsed to LF, trailing whitespace trimmed — rendered as lowercase hex.</summary>
public sealed class MigrationChecksumHasher : IMigrationChecksumHasher
{
    /// <inheritdoc />
    public string Hash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
