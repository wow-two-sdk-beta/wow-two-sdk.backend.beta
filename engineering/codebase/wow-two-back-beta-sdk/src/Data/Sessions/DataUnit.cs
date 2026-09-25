namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Owns one session boundary and its deferred callbacks.</summary>
internal sealed class DataUnit(
    Func<DataUnit, CancellationToken, ValueTask> complete,
    Func<DataUnit, ValueTask> abandon) : IDataUnit
{
    internal string? Savepoint { get; init; }
    internal bool Settled { get; set; }
    internal List<DataSessionHook> CommitHooks { get; } = [];
    internal List<Func<CancellationToken, ValueTask>> RollbackHooks { get; } = [];

    public ValueTask CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Settled)
        {
            throw new InvalidOperationException("The data unit has already settled.");
        }
        return complete(this, cancellationToken);
    }

    public ValueTask AbandonAsync()
    {
        if (Settled)
        {
            throw new InvalidOperationException("The data unit has already settled.");
        }
        return abandon(this);
    }

    public ValueTask DisposeAsync()
    {
        return Settled ? ValueTask.CompletedTask : abandon(this);
    }
}
