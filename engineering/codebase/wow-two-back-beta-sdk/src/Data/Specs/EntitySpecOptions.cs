namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Holds how mappers treat entity specs.</summary>
/// <remarks>Set in code with <c>AddEntitySpecs(…, o => …)</c> or in the host section <c>Data:Specs</c>, which is applied last.</remarks>
public sealed record EntitySpecOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Data:Specs";

    /// <summary>Gets or sets what a mapper does with a feature its backend cannot express. Default <see cref="UnsupportedSpecMode.Throw"/>.</summary>
    public UnsupportedSpecMode Unsupported { get; set; } = UnsupportedSpecMode.Throw;
}
