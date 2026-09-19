using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Refers to what an <see cref="IEventResiliencePipeline"/> does with an exception thrown by a consume attempt.</summary>
public enum FaultDisposition
{
    /// <summary>Retry under the configured schedule, then propagate once attempts are exhausted. The default for every exception.</summary>
    Retry,

    /// <summary>
    /// Propagate immediately without spending an attempt — nothing about a redelivery would change the outcome
    /// (validation, deserialization, a missing contract), so the consume pipeline dead-letters it on the first failure.
    /// </summary>
    DeadLetter,

    /// <summary>
    /// Swallow: the resilience pipeline returns normally, so the consume pipeline takes its success path and
    /// acknowledges the message. For failures that are expected and uninteresting (already-applied, stale-version).
    /// </summary>
    Ignore,
}
