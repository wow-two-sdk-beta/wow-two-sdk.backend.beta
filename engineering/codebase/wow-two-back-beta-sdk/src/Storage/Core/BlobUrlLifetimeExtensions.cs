namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>Holds the lifetime rule every URL issuer shares: positive and at most 7 days, the S3 signing ceiling.</summary>
internal static class BlobUrlLifetimeExtensions
{
    /// <summary>The longest a signed URL may live.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromDays(7);

    /// <summary>Throws when <paramref name="lifetime"/> is not positive or over <see cref="Longest"/>.</summary>
    public static TimeSpan EnsureValid(this TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > Longest)
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "A signed URL lives between one second and 7 days.");

        return lifetime;
    }

    /// <summary>A content disposition that saves as <paramref name="fileName"/>, with an RFC 5987 UTF-8 name.</summary>
    public static string Attachment(string fileName)
    {
        var ascii = new string([.. fileName.Select(character => character is >= ' ' and < '\u007f' and not '"' and not '\\' ? character : '_')]);
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
