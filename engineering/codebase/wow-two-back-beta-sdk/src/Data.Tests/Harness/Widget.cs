using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>An audited widget — the minimal entity the wiring and guard assertions write through.</summary>
public sealed class Widget : IKeyedEntity<Guid>, IAuditable, IHasTableName
{
    /// <summary>The table this entity maps to.</summary>
    public static string TableName => "widgets";

    /// <summary>Row id.</summary>
    public Guid Id { get; set; }

    /// <summary>A mutable payload column, so an update has something to diff.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Creation stamp, written by <c>AuditInterceptor</c>.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Modification stamp, written by <c>AuditInterceptor</c>.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
