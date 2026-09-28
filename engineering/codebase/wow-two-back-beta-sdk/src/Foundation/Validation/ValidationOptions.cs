namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Holds the validation module's options, set with <c>ConfigureValidation(o => …)</c> or the host section <c>Validation</c>.</summary>
public sealed record ValidationOptions
{
    /// <summary>The host configuration section the options bind from, after code configuration.</summary>
    public const string SectionName = "Validation";

    /// <summary>Message translation for validation failures and error responses.</summary>
    public ValidationTranslationOptions Translation { get; } = new();
}
