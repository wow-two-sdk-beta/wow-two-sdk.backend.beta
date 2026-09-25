namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Defines one explicit transaction or savepoint lifetime.</summary>
public interface IDataUnit : IAsyncDisposable
{
    /// <summary>Flushes pending changes and completes this boundary in reverse opening order.</summary>
    /// <param name="cancellationToken">Cancels flushing or database completion.</param>
    ValueTask CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back this boundary using the session's independent cleanup budget.</summary>
    ValueTask AbandonAsync();
}
