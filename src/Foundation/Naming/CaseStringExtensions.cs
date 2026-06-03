namespace WoW.Two.Sdk.Backend.Beta.Naming;

/// <summary>String extension shorthands over <see cref="CaseConverter"/>.</summary>
public static class CaseStringExtensions
{
    /// <summary>Converts the string to <paramref name="style"/>.</summary>
    public static string ToCase(this string? value, CaseStyle style) => CaseConverter.ToCase(value, style);

    /// <summary>Converts the string to <c>snake_case</c>.</summary>
    public static string ToSnakeCase(this string? value) => CaseConverter.ToSnakeCase(value);

    /// <summary>Converts the string to <c>camelCase</c>.</summary>
    public static string ToCamelCase(this string? value) => CaseConverter.ToCamelCase(value);

    /// <summary>Converts the string to <c>PascalCase</c>.</summary>
    public static string ToPascalCase(this string? value) => CaseConverter.ToPascalCase(value);

    /// <summary>Converts the string to <c>kebab-case</c>.</summary>
    public static string ToKebabCase(this string? value) => CaseConverter.ToKebabCase(value);
}
