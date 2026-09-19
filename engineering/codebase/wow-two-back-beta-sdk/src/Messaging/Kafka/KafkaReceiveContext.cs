using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>
/// Kafka <see cref="ReceiveContext"/>. Offset advancement (<c>StoreOffset</c>) is done by the consume loop on the
/// consume thread — librdkafka's consumer is not thread-safe — so acknowledge is a no-op here; dead-letter re-produces
/// the original message to the DLQ topic via the (thread-safe) producer (emulated DLQ).
/// </summary>
internal sealed class KafkaReceiveContext(
    EventEnvelopeModel envelope,
    IProducer<string, byte[]> deadLetterProducer,
    string deadLetterTopic,
    Message<string, byte[]> originalMessage) : ReceiveContext
{
    public override EventEnvelopeModel Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        // Produce a fresh message (don't re-submit the consumed instance) carrying death headers for triage.
        await deadLetterProducer.ProduceAsync(deadLetterTopic, KafkaDeadLetterMapper.Build(originalMessage, reason, exception), cancellationToken);
    }
}
