using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines behavior that watches the send path — notified around the hand-off of an <see cref="EventEnvelopeModel"/> to the
/// <see cref="ISendTransport"/>. Register with <c>AddMessageObservingInterceptor&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
///   - watches only — use <see cref="IConsumeInterceptor"/> to change the outcome
///   - a thrown exception is caught and logged, never propagated
///   - resolved as a singleton — must be thread-safe
/// </remarks>
public interface IPublishObservingInterceptor
{
    /// <summary>The envelope is fully built (ids, transport hints, trace-context headers) and is about to be handed to the transport.</summary>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PrePublishAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken);

    /// <summary>The transport accepted the envelope. What that proves is transport-specific — see <see cref="ITransportCapabilities.NativePublisherConfirms"/>.</summary>
    /// <param name="envelope">The envelope that was sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostPublishAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken);

    /// <summary>The transport threw while sending. The exception still propagates to the caller; this hook only records it. Not raised when the send is cancelled through the caller's own token.</summary>
    /// <param name="envelope">The envelope that failed to send.</param>
    /// <param name="exception">The fault thrown by the transport.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishFaultAsync(EventEnvelopeModel envelope, Exception exception, CancellationToken cancellationToken);
}
