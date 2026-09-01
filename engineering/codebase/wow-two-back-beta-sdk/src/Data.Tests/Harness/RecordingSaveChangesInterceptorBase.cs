using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>Base for the suite's save-recording interceptors — appends <see cref="Tag"/> to the shared log on every save.</summary>
/// <param name="log">The shared invocation log.</param>
public abstract class RecordingSaveChangesInterceptorBase(InterceptorLog log) : SaveChangesInterceptor
{
    /// <summary>The value this interceptor appends to the log.</summary>
    protected abstract string Tag { get; }

    /// <summary>The shared invocation log.</summary>
    protected InterceptorLog Log { get; } = log;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Log.Add(Tag);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Log.Add(Tag);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
