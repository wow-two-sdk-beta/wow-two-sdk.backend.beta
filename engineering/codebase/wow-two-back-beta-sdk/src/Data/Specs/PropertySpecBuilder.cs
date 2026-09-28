namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Builds one <see cref="PropertySpec"/> step by step; <see cref="EntitySpecBuilder{TEntity}.Build"/> collects it.</summary>
public sealed class PropertySpecBuilder
{
    internal PropertySpecBuilder(string name) => Spec = new PropertySpec { Name = name };

    internal PropertySpec Spec { get; private set; }

    /// <summary>Names the column.</summary>
    /// <param name="column">The column name.</param>
    public PropertySpecBuilder HasColumnName(string column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        Spec = Spec with { Column = column };
        return this;
    }

    /// <summary>Marks the column as rejecting nulls, or accepting them.</summary>
    /// <param name="required">Whether nulls are rejected.</param>
    public PropertySpecBuilder IsRequired(bool required = true)
    {
        Spec = Spec with { IsRequired = required };
        return this;
    }

    /// <summary>Caps the length of a string or binary column.</summary>
    /// <param name="maxLength">The maximum length.</param>
    public PropertySpecBuilder HasMaxLength(int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        Spec = Spec with { MaxLength = maxLength };
        return this;
    }

    /// <summary>Sets the digits of a decimal column.</summary>
    /// <param name="precision">The total digits.</param>
    /// <param name="scale">The digits after the decimal point.</param>
    public PropertySpecBuilder HasPrecision(int precision, int? scale = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(precision);
        Spec = Spec with { Precision = precision, Scale = scale };
        return this;
    }

    /// <summary>Marks a string column as Unicode or not.</summary>
    /// <param name="unicode">Whether it stores Unicode.</param>
    public PropertySpecBuilder IsUnicode(bool unicode = true)
    {
        Spec = Spec with { IsUnicode = unicode };
        return this;
    }

    /// <summary>Lets the store fill the column with <paramref name="sql"/> when an insert leaves it out.</summary>
    /// <param name="sql">The default expression.</param>
    public PropertySpecBuilder HasDefaultValueSql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        Spec = Spec with { DefaultValueSql = sql, Generation = ValueGenerationKind.OnAdd };
        return this;
    }

    /// <summary>Lets the store compute the column from <paramref name="sql"/>.</summary>
    /// <param name="sql">The computed expression.</param>
    public PropertySpecBuilder HasComputedColumnSql(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        Spec = Spec with { ComputedSql = sql, Generation = ValueGenerationKind.OnAddOrUpdate };
        return this;
    }

    /// <summary>Lets the store write the value on insert.</summary>
    public PropertySpecBuilder ValueGeneratedOnAdd()
    {
        Spec = Spec with { Generation = ValueGenerationKind.OnAdd };
        return this;
    }

    /// <summary>Lets the store write the value on insert and on every update.</summary>
    public PropertySpecBuilder ValueGeneratedOnAddOrUpdate()
    {
        Spec = Spec with { Generation = ValueGenerationKind.OnAddOrUpdate };
        return this;
    }

    internal void Ignore() => Spec = Spec with { IsIgnored = true };
}
