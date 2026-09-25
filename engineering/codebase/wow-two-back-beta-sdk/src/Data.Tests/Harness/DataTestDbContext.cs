using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

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

        modelBuilder.Entity<XminWidget>(entity =>
        {
            entity.ToTable(XminWidget.TableName);
            entity.HasKey(widget => widget.Id);
            entity.Property(widget => widget.Xmin).IsRowVersion();
        });

        // The outbox entity's EF configuration is internal to the SDK assembly — apply it explicitly.
        modelBuilder.ApplyOutboxModel();
    }
}
