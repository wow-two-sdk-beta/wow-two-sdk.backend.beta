using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// A dead-letter store that can also be searched, corrected and emptied — everything an operator console needs beyond
/// "park it" and "put it back".
/// </summary>
/// <remarks>
///   - optional — <see cref="IDeadLetterAdmin"/> runs over a bare <see cref="IDeadLetterRepository"/> without it
///   - browse then stays within named sources
///   - by-id lookup and purge require it
/// </remarks>
public interface IDeadLetterQueryRepository : IDeadLetterRepository
{
    /// <summary>Enumerate records matching <paramref name="query"/>, across every source when it names none.</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> QueryAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Look a record up by message id without knowing its source; <c>null</c> when the store holds none.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> FindAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Overwrite a stored record in place (redrive marker, quarantine state); <c>false</c> when it is no longer there.</summary>
    /// <param name="record">The record to store, keyed by its message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> UpdateAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Delete a record without replaying it; <c>false</c> when it is no longer there.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> RemoveAsync(string messageId, CancellationToken cancellationToken);
}
