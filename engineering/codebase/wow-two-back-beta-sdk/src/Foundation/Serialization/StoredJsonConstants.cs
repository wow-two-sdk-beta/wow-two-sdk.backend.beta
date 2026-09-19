using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

/// <summary>Holds the options every stored JSON document is written and read with — camelCase, string enums, NodaTime, nulls written, metadata in any order.</summary>
public static class StoredJsonConstants
{
    /// <summary>Gets the stored preset with no union modifiers — enough for a document whose polymorphic bases carry <see cref="JsonDerivedTypeAttribute"/>.</summary>
    public static JsonSerializerOptions Default { get; } = StoredJsonOptionsFactory.Create();
}
