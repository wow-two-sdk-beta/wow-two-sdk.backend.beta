namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Represents how one property maps to a column, independent of the data-access library.</summary>
public sealed record PropertySpec
{
    /// <summary>The CLR property name.</summary>
    public required string Name { get; init; }

    /// <summary>The column name; null lets the mapper's naming convention decide.</summary>
    public string? Column { get; init; }

    /// <summary>Whether the column rejects nulls; null keeps the mapper's inference.</summary>
    public bool? IsRequired { get; init; }

    /// <summary>The maximum length of a string or binary column.</summary>
    public int? MaxLength { get; init; }

    /// <summary>The total digits of a decimal column.</summary>
    public int? Precision { get; init; }

    /// <summary>The digits after the decimal point.</summary>
    public int? Scale { get; init; }

    /// <summary>Whether a string column stores Unicode.</summary>
    public bool? IsUnicode { get; init; }

    /// <summary>SQL the store evaluates for a column the insert leaves out.</summary>
    public string? DefaultValueSql { get; init; }

    /// <summary>SQL the store computes the column from.</summary>
    public string? ComputedSql { get; init; }

    /// <summary>When the store writes the value.</summary>
    public ValueGenerationKind Generation { get; init; }

    /// <summary>Whether the property has no column.</summary>
    public bool IsIgnored { get; init; }
}
