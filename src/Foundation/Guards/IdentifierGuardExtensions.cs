using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace WoW.Two.Sdk.Backend.Beta.Guards;

/// <summary>
/// Custom guard extensions on top of <see cref="IGuardClause"/>.
/// </summary>
public static partial class IdentifierGuardExtensions
{
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.CultureInvariant)]
    private static partial Regex UlidRegex();

    /// <summary>
    /// Throws if <paramref name="input"/> isn't a valid slug (kebab-case, no leading/trailing hyphens).
    /// </summary>
    public static string NotSlug(this IGuardClause guard, string input, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(guard);
        Guard.Against.NullOrWhiteSpace(input, parameterName);
        if (!SlugRegex().IsMatch(input))
            throw new ArgumentException($"'{parameterName}' is not a valid slug (lowercase kebab-case).", parameterName);
        return input;
    }

    /// <summary>
    /// Throws if <paramref name="input"/> isn't a Crockford-base32 ULID (26 chars).
    /// </summary>
    public static string NotUlid(this IGuardClause guard, string input, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(guard);
        Guard.Against.NullOrWhiteSpace(input, parameterName);
        if (!UlidRegex().IsMatch(input))
            throw new ArgumentException($"'{parameterName}' is not a valid ULID.", parameterName);
        return input;
    }
}
