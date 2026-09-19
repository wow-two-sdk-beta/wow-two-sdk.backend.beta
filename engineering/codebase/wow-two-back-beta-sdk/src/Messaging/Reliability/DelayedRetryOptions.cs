using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Holds settings that move the retry backoff out of the consume slot: instead of sleeping in-process with the message unsettled, the
/// failed delivery is re-published with a future <see cref="EventEnvelopeModel.NotBeforeUtc"/> and then acknowledged, so the
/// consumer is free again for the whole of the wait.
/// </summary>
/// <remarks>
///   - each attempt is a fresh delivery, so the whole consume filter chain re-runs, not only the core
///   - each attempt acknowledges the failed delivery, so broker redelivery timers, prefetch accounting and in-flight metrics see it leave and come back
///   - a custom <see cref="IEventResiliencePipeline"/> must stop after one attempt, or a message pays both loops
/// </remarks>
public sealed record DelayedRetryOptions
{
    /// <summary>
    /// Re-enqueue a failed message with a delay instead of waiting in the consume slot. Defaults to <c>false</c> —
    /// today's in-process behaviour — so binding a configuration section that omits the key never changes delivery
    /// semantics. <c>AddDelayedEventRetry(...)</c> turns it on before applying the caller's configuration.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Retry schedule for the re-enqueue loop. <c>null</c> (default) shares the schedule the in-process pipeline uses
    /// (<see cref="InMemoryEventBusOptions.Retry"/>), so switching the wait's location does not silently change the
    /// policy. Set it to give the re-enqueue loop its own budget — the practical case being a Polly-backed pipeline,
    /// whose retry strategy is bypassed here and whose attempt count therefore has no say.
    /// </summary>
    public RetryConfig? Retry { get; set; }
}
