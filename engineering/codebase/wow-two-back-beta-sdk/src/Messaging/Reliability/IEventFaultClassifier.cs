using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Sorts a thrown exception into a <see cref="FaultDisposition"/> so the resilience pipeline can skip the retry budget
/// for failures that cannot succeed on redelivery. Every <see cref="IEventResiliencePipeline"/> implementation consults
/// the same registered classifier, so behaviour does not depend on which pipeline is registered.
/// </summary>
/// <remarks>
/// Implementations MUST be thread-safe and SHOULD NOT throw — <see cref="Classify"/> runs on the consume path for every
/// failed attempt, and one pipeline evaluates it inside an exception filter.
/// </remarks>
public interface IEventFaultClassifier
{
    /// <summary>Classify <paramref name="exception"/>; return <see cref="FaultDisposition.Retry"/> when there is no reason to treat it specially.</summary>
    /// <param name="exception">The exception thrown by the attempt.</param>
    FaultDisposition Classify(Exception exception);
}
