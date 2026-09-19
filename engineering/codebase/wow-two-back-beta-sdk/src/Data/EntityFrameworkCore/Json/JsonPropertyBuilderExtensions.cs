using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>Helpers for mapping a CLR property to a JSON column with the SDK's converter + comparer.</summary>
public static class JsonPropertyBuilderExtensions
{
    /// <summary>Maps the property as JSON under the stored preset; pair with the provider-specific column type — <c>jsonb</c> on Postgres, <c>nvarchar(max)</c> on SqlServer.</summary>
    /// <typeparam name="T">The CLR type stored as JSON.</typeparam>
    /// <param name="builder">The property builder for the property mapped to JSON.</param>
    /// <param name="options">A pinned instance built with <see cref="StoredJsonOptionsFactory"/> for a root whose unions are declared outside its types; null takes <see cref="StoredJsonConstants.Default"/>.</param>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var stored = options ?? StoredJsonConstants.Default;
        builder.HasConversion(new JsonValueConverter<T>(stored));
        builder.Metadata.SetValueComparer(new JsonValueComparer<T>(stored));
        return builder;
    }
}
