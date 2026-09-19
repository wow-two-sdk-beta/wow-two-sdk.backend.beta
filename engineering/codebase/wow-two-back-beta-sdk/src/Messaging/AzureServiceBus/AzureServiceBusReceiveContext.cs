using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>
/// Azure Service Bus <see cref="ReceiveContext"/> — settles through the receiver that delivered the message, which is
/// what lets settlement run from a pump worker rather than on the consume loop. Dead-lettering is native: the broker
/// moves the message to the entity's <c>$DeadLetterQueue</c> sub-queue and records the reason on it, so nothing is
/// re-published onto a second queue the way an emulated DLQ has to.
/// </summary>
internal sealed class AzureServiceBusReceiveContext(
    EventEnvelopeModel envelope,
    ServiceBusReceiver receiver,
    ServiceBusReceivedMessage message) : ReceiveContext
{
    /// <summary>Service Bus ceiling on the dead-letter reason and description properties.</summary>
    private const int MaxDeadLetterTextLength = 4096;

    public override EventEnvelopeModel Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => new(receiver.CompleteMessageAsync(message, cancellationToken));

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
        // Native dead-letter with reason + description, so the record rides the message in the DLQ itself.
        => new(receiver.DeadLetterMessageAsync(
            message,
            Truncate(reason),
            Truncate(exception is null ? reason : $"{exception.GetType().FullName}: {exception.Message}"),
            cancellationToken));

    /// <summary>Clip text to the Service Bus limit — an over-long reason fails the settle call, turning a dead-letter into a lock timeout and a redelivery.</summary>
    private static string Truncate(string text)
        => text.Length <= MaxDeadLetterTextLength ? text : text[..MaxDeadLetterTextLength];
}
