using System.Reflection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>Bulk registration of CLR enums as PostgreSQL enum types on an <see cref="NpgsqlDataSourceBuilder"/> — the PG type name and each member label are derived from the enum via the SDK casing engine, so the driver-level mapping and any string-based mapping agree by construction.</summary>
/// <remarks>Replaces the hand-maintained "list every enum twice" registry pattern: call <see cref="MapEnums"/> once with the assembly (or assemblies) holding your enums; Npgsql requires enum mappings at the data-source (driver) level, so this runs on the builder before <c>Build()</c>.</remarks>
public static class NpgsqlEnumMappingExtensions
{
    /// <summary>Maps every enum in <paramref name="assemblies"/> whose namespace passes <paramref name="namespaceFilter"/> (default: all) as a PG enum, with type name and member labels using <paramref name="style"/> (default <see cref="CaseStyle.Snake"/>) and the PG type name customizable per enum via <paramref name="pgTypeName"/>.</summary>
    /// <param name="builder">The data-source builder to register mappings on.</param>
    /// <param name="style">Casing for the PG type name and member labels. Default <see cref="CaseStyle.Snake"/>.</param>
    /// <param name="namespaceFilter">Optional predicate on the enum's namespace — return <c>true</c> to include. Default includes all.</param>
    /// <param name="pgTypeName">Optional override for the PG type name, given the enum <see cref="Type"/>. Return <c>null</c> to use the styled default.</param>
    /// <param name="assemblies">The assemblies to scan. Must be non-empty.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static NpgsqlDataSourceBuilder MapEnums(
        this NpgsqlDataSourceBuilder builder,
        CaseStyle style = CaseStyle.Snake,
        Func<string, bool>? namespaceFilter = null,
        Func<Type, string?>? pgTypeName = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

        var translator = new CaseStyleNameTranslator(style);

        foreach (var enumType in DiscoverEnums(assemblies, namespaceFilter))
        {
            var name = pgTypeName?.Invoke(enumType) ?? CaseConverter.ToCase(enumType.Name, style);
            builder.MapEnum(enumType, name, translator);
        }

        return builder;
    }

    /// <summary>Enumerates the public, non-nested enums across <paramref name="assemblies"/> matching the namespace filter.</summary>
    internal static IEnumerable<Type> DiscoverEnums(IReadOnlyList<Assembly> assemblies, Func<string, bool>? namespaceFilter)
    {
        var filter = namespaceFilter ?? (static _ => true);
        var seen = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type is { IsEnum: true, IsNested: false }
                    && filter(type.Namespace ?? string.Empty)
                    && seen.Add(type))
                {
                    yield return type;
                }
            }
        }
    }
}
