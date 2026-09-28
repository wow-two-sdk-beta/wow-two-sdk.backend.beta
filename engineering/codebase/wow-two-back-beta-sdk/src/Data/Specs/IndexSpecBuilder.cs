namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Builds one <see cref="IndexSpec"/> step by step; <see cref="EntitySpecBuilder{TEntity}.Build"/> collects it.</summary>
public sealed class IndexSpecBuilder
{
    internal IndexSpecBuilder(IReadOnlyList<string> properties) => Spec = new IndexSpec { Properties = properties };

    internal IndexSpec Spec { get; private set; }

    /// <summary>Makes the index reject duplicates.</summary>
    /// <param name="unique">Whether duplicates are rejected.</param>
    public IndexSpecBuilder IsUnique(bool unique = true)
    {
        Spec = Spec with { IsUnique = unique };
        return this;
    }

    /// <summary>Names the index.</summary>
    /// <param name="name">The index name.</param>
    public IndexSpecBuilder HasName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Spec = Spec with { Name = name };
        return this;
    }

    /// <summary>Limits the index to rows matching <paramref name="sql"/>.</summary>
    /// <param name="sql">The predicate in the store's SQL.</param>
    public IndexSpecBuilder HasFilter(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        Spec = Spec with { Filter = sql };
        return this;
    }
}
