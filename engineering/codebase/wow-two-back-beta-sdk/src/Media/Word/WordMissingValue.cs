namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Refers to what a template does with a placeholder the data lacks.</summary>
public enum WordMissingValue
{
    /// <summary>Refers to refusing the fill, naming the missing placeholders.</summary>
    Reject,

    /// <summary>Refers to leaving the placeholder text in place, visible for review.</summary>
    Keep,

    /// <summary>Refers to removing the placeholder.</summary>
    Empty,
}
