namespace WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

/// <summary>String extension shorthands over <see cref="CaseConverter"/>.</summary>
public static class CaseStringExtensions
{
    /// <summary>Converts the string to <paramref name="style"/>.</summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="style">The target casing style.</param>
    public static string ToCase(this string? value, CaseStyle style) => CaseConverter.ToCase(value, style);

    /// <summary>Converts the string to <c>snake_case</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToSnakeCase(this string? value) => CaseConverter.ToSnakeCase(value);

    /// <summary>Converts the string to <c>camelCase</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToCamelCase(this string? value) => CaseConverter.ToCamelCase(value);

    /// <summary>Converts the string to <c>PascalCase</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToPascalCase(this string? value) => CaseConverter.ToPascalCase(value);

    /// <summary>Converts the string to <c>kebab-case</c>.</summary>
    /// <param name="value">The string to convert.</param>
    public static string ToKebabCase(this string? value) => CaseConverter.ToKebabCase(value);
}
