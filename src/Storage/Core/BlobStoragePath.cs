namespace WoW.Two.Sdk.Backend.Beta.Storage.Core;

/// <summary>Normalizes and validates logical blob paths, guarding against directory traversal.</summary>
public static class BlobStoragePath
{
    /// <summary>
    /// Normalizes a logical blob path: back-slashes to forward-slashes, trimmed leading slashes, and
    /// rejects empty, <c>.</c>, or <c>..</c> segments (path-traversal defence).
    /// </summary>
    /// <param name="path">The raw logical path.</param>
    /// <returns>The normalized forward-slash path.</returns>
    /// <exception cref="ArgumentException">The path is empty or contains an unsafe segment.</exception>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/').TrimStart('/');
        foreach (var segment in normalized.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new ArgumentException($"Blob path '{path}' contains an invalid or unsafe segment.", nameof(path));
        }

        return normalized;
    }

    /// <summary>Resolves a logical path to an absolute filesystem path under <paramref name="rootFullPath"/>, ensuring it cannot escape the root.</summary>
    /// <param name="rootFullPath">The absolute store root directory.</param>
    /// <param name="path">The logical blob path.</param>
    /// <returns>The absolute filesystem path.</returns>
    /// <exception cref="ArgumentException">The resolved path would fall outside the root.</exception>
    public static string ResolveUnderRoot(string rootFullPath, string path)
    {
        var normalized = Normalize(path);
        var combined = System.IO.Path.GetFullPath(System.IO.Path.Combine(rootFullPath, normalized));

        var rootWithSeparator = rootFullPath.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + System.IO.Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new ArgumentException($"Blob path '{path}' resolves outside the storage root.", nameof(path));

        return combined;
    }
}
