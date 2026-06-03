using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>
/// Helpers for mapping a CLR property to a JSON column with the SDK's converter + comparer.
/// </summary>
public static class JsonPropertyBuilderExtensions
{
    /// <summary>
    /// Maps the property as JSON. Pair with the provider-specific column type:
    /// <c>jsonb</c> on Postgres, <c>nvarchar(max)</c> on SqlServer.
    /// </summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .HasConversion(new JsonValueConverter<T>())
            .Metadata is { } metadata
            ? Apply(builder, metadata)
            : builder;
    }

    private static PropertyBuilder<T> Apply<T>(PropertyBuilder<T> builder, Microsoft.EntityFrameworkCore.Metadata.IMutableProperty metadata)
    {
        metadata.SetValueComparer(new JsonValueComparer<T>());
        return builder;
    }
}
