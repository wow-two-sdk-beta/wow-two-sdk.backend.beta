namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>Normalizes user names and emails to a canonical form for case-insensitive lookup keys.</summary>
public interface ILookupNormalizer
{
    /// <summary>Return the normalized form of <paramref name="key"/> (null in → null out).</summary>
    /// <param name="key">The value to normalize.</param>
    string? Normalize(string? key);
}

/// <summary>Default normalizer — Unicode-normalizes then upper-cases invariantly (matches ASP.NET Identity).</summary>
public sealed class UpperInvariantLookupNormalizer : ILookupNormalizer
{
    /// <inheritdoc />
    public string? Normalize(string? key) => key?.Normalize().ToUpperInvariant();
}
