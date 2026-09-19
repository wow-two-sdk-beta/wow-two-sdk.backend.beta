using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Defines operator surface over a dead-letter store: browse, peek, redrive (singly and in bulk), quarantine, release, purge.
/// A DLQ is otherwise write-only — this is what turns it into something a human can triage.
/// </summary>
/// <remarks>
///   - transport-neutral — every operation routes through <see cref="IDeadLetterRepository"/>
///   - by-id lookup and purge need <see cref="IDeadLetterQueryRepository"/>, else <see cref="RedriveOutcome.NotSupported"/> or a <see cref="NotSupportedException"/>
///   - redrive stamps the marker on the stored record, then replays via <see cref="IDeadLetterRepository.ReplayAsync"/>
/// </remarks>
public interface IDeadLetterAdmin
{
    /// <summary>Enumerate dead-lettered messages matching <paramref name="query"/>, newest first where the store can order.</summary>
    /// <param name="query">The filter; a default instance matches everything up to its limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> BrowseAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Read one record by message id. Requires an <see cref="IDeadLetterQueryRepository"/>; returns <c>null</c> otherwise.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> PeekAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Read one record by source and message id — the form that works on any store.</summary>
    /// <param name="source">The source destination/queue name.</param>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> PeekAsync(string source, string messageId, CancellationToken cancellationToken);

    /// <summary>Redrive a record already in hand — the browse-then-replay path, and the one form that needs nothing beyond the floor store.</summary>
    /// <param name="record">The record to put back.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<RedriveOutcome> RedriveAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Redrive by message id. Requires an <see cref="IDeadLetterQueryRepository"/> to resolve the id.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<RedriveOutcome> RedriveAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Redrive every record matching <paramref name="query"/>, capped by its limit.</summary>
    /// <param name="query">The filter selecting what to put back.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRedriveResult> RedriveAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Quarantine matching records — hold them back from browse and redrive; returns how many changed state.</summary>
    /// <param name="query">The filter selecting what to hold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> QuarantineAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Release matching records back to <see cref="DeadLetterState.DeadLettered"/>; returns how many changed state.</summary>
    /// <param name="query">The filter; set <see cref="DeadLetterQuery.QuarantinedOnly"/> to target the held set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> ReleaseAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Delete matching records without replaying them; returns how many were removed.</summary>
    /// <param name="query">The filter selecting what to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">The registered store is not an <see cref="IDeadLetterQueryRepository"/> and cannot delete.</exception>
    ValueTask<int> PurgeAsync(DeadLetterQuery query, CancellationToken cancellationToken);
}
