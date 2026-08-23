namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>Extends casing with the shorthands a caller reaches for on a string.</summary>
public static class CasingExtensions
{
    /// <summary>Converts the string to <paramref name="style"/>.</summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="style">The target casing style.</param>
    public static string ToCase(this string? value, CaseStyle style) => CaseMapper.ToCase(value, style);

    /// <summary>Converts the string to <c>snake_case</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToSnakeCase(this string? value) => CaseMapper.ToSnakeCase(value);

    /// <summary>Converts the string to <c>camelCase</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToCamelCase(this string? value) => CaseMapper.ToCamelCase(value);

    /// <summary>Converts the string to <c>PascalCase</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToPascalCase(this string? value) => CaseMapper.ToPascalCase(value);

    /// <summary>Converts the string to <c>kebab-case</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToKebabCase(this string? value) => CaseMapper.ToKebabCase(value);

    /// <summary>Canonicalizes the string so two spellings of one value compare equal — null in, null out.</summary>
    /// <param name="value">The value to canonicalize.</param>
    /// <returns>The Unicode-normalized, invariantly upper-cased form.</returns>
    public static string? ToCanonical(this string? value) => value?.Normalize().ToUpperInvariant();
}
