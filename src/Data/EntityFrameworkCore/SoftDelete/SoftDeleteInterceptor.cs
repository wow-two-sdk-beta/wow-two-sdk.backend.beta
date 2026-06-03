using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Audit;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SoftDelete;

/// <summary>Rewrites a delete into a soft-delete for <see cref="ISoftDeletable"/> entities — sets <c>IsDeleted</c>, <c>DeletedAt</c>, and (when a user is resolved) <c>DeletedBy</c>.</summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;
    private readonly IAuditCurrentUserAccessor? _userAccessor;

    /// <summary>Initializes a new instance of the <see cref="SoftDeleteInterceptor"/> class.</summary>
    public SoftDeleteInterceptor(TimeProvider timeProvider, IAuditCurrentUserAccessor? userAccessor = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _userAccessor = userAccessor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Soften(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Soften(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Soften(DbContext? context)
    {
        if (context is null) return;

        var now = _timeProvider.GetUtcNow();
        var userId = _userAccessor?.GetCurrentUserId();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Deleted) continue;
            if (entry.Entity is not ISoftDeletable softDeletable) continue;

            entry.State = EntityState.Modified;
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAt = now;

            if (userId is not null && entry.Entity is ISoftDeletableBy<Guid> deletableBy)
                deletableBy.DeletedBy = userId.Value;

            // EF marks the aggregate's owned types Deleted alongside the parent; keep them Modified so their columns persist.
            foreach (var reference in entry.References)
                if (reference.TargetEntry is { State: EntityState.Deleted } owned && owned.Metadata.IsOwned())
                    owned.State = EntityState.Modified;
        }
    }
}
