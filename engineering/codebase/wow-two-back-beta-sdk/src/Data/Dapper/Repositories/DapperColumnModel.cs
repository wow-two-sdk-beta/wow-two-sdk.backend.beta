using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Dapper.Repositories;

/// <summary>Represents one property's column and whether the store writes it.</summary>
internal sealed class DapperColumnModel
{
    /// <summary>The CLR property.</summary>
    public required PropertyInfo Property { get; init; }

    /// <summary>The column name.</summary>
    public required string Column { get; init; }

    /// <summary>Whether inserts leave the column to the store.</summary>
    public required bool InsertExcluded { get; init; }

    /// <summary>Whether updates leave the column to the store.</summary>
    public required bool UpdateExcluded { get; init; }
}
