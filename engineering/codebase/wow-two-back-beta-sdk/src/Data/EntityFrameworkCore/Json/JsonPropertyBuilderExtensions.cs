using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>Helpers for mapping a CLR property to a JSON column with the SDK's converter + comparer.</summary>
public static class JsonPropertyBuilderExtensions
{
    /// <summary>Maps the property as JSON; pair with the provider-specific column type — <c>jsonb</c> on Postgres, <c>nvarchar(max)</c> on SqlServer.</summary>
    /// <typeparam name="T">The CLR type stored as JSON.</typeparam>
    /// <param name="builder">The property builder for the property mapped to JSON.</param>
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
