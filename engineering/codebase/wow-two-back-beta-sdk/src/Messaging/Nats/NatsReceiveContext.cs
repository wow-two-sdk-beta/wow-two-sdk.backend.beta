using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>
/// NATS JetStream <see cref="ReceiveContext"/>. NATS.Net is thread-safe, so (unlike Kafka) the message is settled here:
/// acknowledge acks the JetStream message; dead-letter re-publishes the original to the dead-letter subject (emulated DLQ)
/// then acks so the broker stops redelivering the poison message.
/// </summary>
internal sealed class NatsReceiveContext(
    EventEnvelope envelope,
    NatsJSContext js,
    string deadLetterSubject,
    NatsJSMsg<byte[]> message) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => message.AckAsync(cancellationToken: cancellationToken);

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        var headers = NatsWireFormatMapper.BuildDeadLetterHeaders(message.Headers, reason, exception);
        await js.PublishAsync(deadLetterSubject, message.Data ?? [], headers: headers, cancellationToken: cancellationToken);
        await message.AckAsync(cancellationToken: cancellationToken);
    }
}
