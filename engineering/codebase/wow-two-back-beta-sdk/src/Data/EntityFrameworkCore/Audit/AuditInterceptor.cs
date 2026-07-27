using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

/// <summary>Stamps creation and modification audit fields on entities implementing the audit interfaces.</summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;
    private readonly IAuditCurrentUserAccessor? _userAccessor;

    /// <summary>Initializes a new instance of the <see cref="AuditInterceptor"/> class.</summary>
    /// <param name="timeProvider">The clock used for audit timestamps; falls back to the system clock when null.</param>
    /// <param name="userAccessor">The accessor resolving the current user id for <c>CreatedBy</c> and <c>UpdatedBy</c> stamping.</param>
    public AuditInterceptor(TimeProvider timeProvider, IAuditCurrentUserAccessor? userAccessor = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _userAccessor = userAccessor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;

        var now = _timeProvider.GetUtcNow();
        var userId = _userAccessor?.GetCurrentUserId();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            StampTimestamps(entry, now);
            StampUserIds(entry, userId);
        }
    }

    private static void StampTimestamps(EntityEntry entry, DateTimeOffset now)
    {
        if (entry.State == EntityState.Added)
        {
            if (entry.Entity is ICreationAuditable created && created.CreatedAt == default)
                created.CreatedAt = now;

            if (entry.Entity is IModificationAuditable modified && modified.UpdatedAt == default)
                modified.UpdatedAt = now;
        }
        else
        {
            if (entry.Entity is IModificationAuditable modified)
                modified.UpdatedAt = now;

            // Preserve CreatedAt — never overwrite on update.
            if (entry.Entity is ICreationAuditable)
                entry.Property(nameof(ICreationAuditable.CreatedAt)).IsModified = false;
        }
    }

    private static void StampUserIds(EntityEntry entry, Guid? userId)
    {
        if (userId is null) return;

        var id = userId.Value;

        if (entry.State == EntityState.Added)
        {
            if (entry.Entity is ICreationAuditableBy<Guid> createdBy && createdBy.CreatedBy == default)
                createdBy.CreatedBy = id;

            if (entry.Entity is IModificationAuditableBy<Guid> modifiedBy)
                modifiedBy.UpdatedBy = id;
        }
        else
        {
            if (entry.Entity is IModificationAuditableBy<Guid> modifiedBy)
                modifiedBy.UpdatedBy = id;

            // Preserve CreatedBy — never overwrite on update.
            if (entry.Entity is ICreationAuditableBy<Guid>)
                entry.Property(nameof(ICreationAuditableBy<Guid>.CreatedBy)).IsModified = false;
        }
    }
}
