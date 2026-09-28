namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Refers to when the store, not the application, writes a column's value.</summary>
public enum ValueGenerationKind
{
    /// <summary>The application writes the value; the mapper's default applies.</summary>
    None,

    /// <summary>The store writes it on insert (identity, default).</summary>
    OnAdd,

    /// <summary>The store writes it on insert and on every update (computed, timestamps).</summary>
    OnAddOrUpdate,
}
