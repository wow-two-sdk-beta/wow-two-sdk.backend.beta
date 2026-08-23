using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper;

/// <summary>Holds the casing a caller wants applied to generated SQL identifiers.</summary>
/// <remarks>
/// Supplied in code through <c>AddDapperConventions</c>, so it is an <c>Options</c> rather than a bound section.
/// Read once per repository instance; nothing mutates it after registration.
/// </remarks>
public sealed record SqlNamingOptions
{
    /// <summary>Gets the casing applied to column names.</summary>
    public CaseStyle ColumnCase { get; set; } = CaseStyle.Snake;

    /// <summary>Gets the casing applied to Dapper parameter names (without the <c>@</c>).</summary>
    public CaseStyle ParameterCase { get; set; } = CaseStyle.Camel;
}
