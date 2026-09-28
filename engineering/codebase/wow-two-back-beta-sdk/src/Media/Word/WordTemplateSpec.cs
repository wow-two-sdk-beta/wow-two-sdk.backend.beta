using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents the values a template is filled with.</summary>
public sealed record WordTemplateSpec
{
    /// <summary>
    /// Gets the values: a JSON object whose paths the placeholders name, such as <c>{{Customer.Name}}</c> or
    /// <c>{{Lines.0.Sku}}</c>; names match case-insensitively. Build one from any object with
    /// <c>JsonSerializer.SerializeToElement(data)</c>.
    /// </summary>
    public required JsonElement Data { get; init; }

    /// <summary>Gets what a placeholder the data lacks becomes. Default <see cref="WordMissingValue.Reject"/>.</summary>
    public WordMissingValue MissingValues { get; init; } = WordMissingValue.Reject;
}
