using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Defines behavior that schedules an envelope for delayed (future) delivery.</summary>
/// <remarks>
///   - carries delayed retry too (<see cref="DelayedRetryOptions"/>) — a dropped envelope drops the retry
///   - only a durable implementation keeps re-enqueued retries across a restart
///   - used only on a transport reporting <c>NativeDelay</c>/<c>NativeScheduling</c>
/// </remarks>
public interface IDelayedDeliveryService
{
    /// <summary>Schedule an envelope to become deliverable at or after <paramref name="notBeforeUtc"/>.</summary>
    /// <param name="envelope">The envelope to schedule.</param>
    /// <param name="notBeforeUtc">Earliest delivery time (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ScheduleAsync(EventEnvelopeModel envelope, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken);
}
