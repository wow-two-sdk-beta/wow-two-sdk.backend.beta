using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;

/// <summary>
/// Defines deciding a thrown exception's <see cref="FaultDisposition"/> so the resilience pipeline can skip the retry budget
/// for failures that cannot succeed on redelivery. Every <see cref="IEventResiliencePipeline"/> implementation consults
/// the same registered policy, so behaviour does not depend on which pipeline is registered.
/// </summary>
/// <remarks>
/// Implementations MUST be thread-safe and SHOULD NOT throw — <see cref="Decide"/> runs on the consume path for every
/// failed attempt, and one pipeline evaluates it inside an exception filter.
/// </remarks>
public interface IEventFaultPolicy
{
    /// <summary>Decides how to handle <paramref name="exception"/>; returns <see cref="FaultDisposition.Retry"/> when there is no reason to treat it specially.</summary>
    /// <param name="exception">The exception thrown by the attempt.</param>
    /// <returns>The retry, dead-letter or ignore decision.</returns>
    FaultDisposition Decide(Exception exception);
}
