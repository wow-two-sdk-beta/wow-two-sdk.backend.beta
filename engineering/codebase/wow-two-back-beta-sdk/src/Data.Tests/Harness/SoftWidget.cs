using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>A soft-deletable widget — carries the SDK's contract-driven query filter and soft-delete rewrite.</summary>
public sealed class SoftWidget : IKeyedEntity<Guid>, ISoftDeletable, IHasTableName
{
    /// <summary>The table this entity maps to.</summary>
    public static string TableName => "soft_widgets";

    /// <summary>Row id.</summary>
    public Guid Id { get; set; }

    /// <summary>A mutable payload column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
