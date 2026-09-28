namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>Refers to how a concurrency token changes on every write.</summary>
public enum ConcurrencyTokenKind
{
    /// <summary>A portable number the SDK increments on each write (<c>IVersioned</c>).</summary>
    Counter,

    /// <summary>A store-generated binary version (SQL Server <c>rowversion</c>).</summary>
    RowVersion,

    /// <summary>PostgreSQL's <c>xmin</c> system column.</summary>
    Xmin,

    /// <summary>A string the SDK replaces with a fresh GUID on each write, such as a <c>ConcurrencyStamp</c>.</summary>
    Stamp,
}
