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

/// <summary>The suite's context — an <see cref="AppDbContextBase"/> so it flows through the SDK's own registration path, plus the outbox mapping the PR4 collision cases need.</summary>
/// <param name="options">The options configuring this context.</param>
public sealed class DataTestDbContext(DbContextOptions<DataTestDbContext> options) : AppDbContextBase(options)
{
    /// <summary>The audited widgets.</summary>
    public DbSet<Widget> Widgets => Set<Widget>();

    /// <summary>The soft-deletable widgets.</summary>
    public DbSet<SoftWidget> SoftWidgets => Set<SoftWidget>();

    /// <summary>The transactional outbox rows the claim strategy competes over.</summary>
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder); // applies the SDK's contract-driven conventions (soft-delete filter, IVersioned token)

        modelBuilder.Entity<Widget>(entity =>
        {
            entity.ToTable(Widget.TableName);
            entity.HasKey(widget => widget.Id);
            entity.Property(widget => widget.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<SoftWidget>(entity =>
        {
            entity.ToTable(SoftWidget.TableName);
            entity.HasKey(widget => widget.Id);
            entity.Property(widget => widget.Name).HasMaxLength(200);
        });

        // The outbox entity's EF configuration is internal to the SDK assembly — apply it explicitly.
        modelBuilder.ApplyOutboxModel();
    }
}
