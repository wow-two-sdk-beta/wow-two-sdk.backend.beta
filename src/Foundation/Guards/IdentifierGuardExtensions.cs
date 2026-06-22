using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Guards;

/// <summary>Provides custom guard extensions on top of <see cref="IGuardClause"/>.</summary>
public static partial class IdentifierGuardExtensions
{
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.CultureInvariant)]
    private static partial Regex UlidRegex();

    /// <summary>Adds a guard that throws if <paramref name="input"/> isn't a valid slug (kebab-case, no leading/trailing hyphens).</summary>
    /// <param name="guard">The guard-clause entry point being extended.</param>
    /// <param name="input">The candidate slug to validate.</param>
    /// <param name="parameterName">The name of the guarded argument, reported on failure.</param>
    public static string NotSlug(this IGuardClause guard, string input, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(guard);
        Guard.Against.NullOrWhiteSpace(input, parameterName);
        if (!SlugRegex().IsMatch(input))
            throw new ArgumentException($"'{parameterName}' is not a valid slug (lowercase kebab-case).", parameterName);
        return input;
    }

    /// <summary>Adds a guard that throws if <paramref name="input"/> isn't a Crockford-base32 ULID (26 chars).</summary>
    /// <param name="guard">The guard-clause entry point being extended.</param>
    /// <param name="input">The candidate ULID to validate.</param>
    /// <param name="parameterName">The name of the guarded argument, reported on failure.</param>
    public static string NotUlid(this IGuardClause guard, string input, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(guard);
        Guard.Against.NullOrWhiteSpace(input, parameterName);
        if (!UlidRegex().IsMatch(input))
            throw new ArgumentException($"'{parameterName}' is not a valid ULID.", parameterName);
        return input;
    }
}
