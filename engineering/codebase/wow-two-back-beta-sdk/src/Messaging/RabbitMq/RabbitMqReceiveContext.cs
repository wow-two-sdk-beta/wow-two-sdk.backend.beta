using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;

/// <summary>RabbitMQ <see cref="ReceiveContext"/> — acknowledge / dead-letter via the delivering channel (nack→DLX).</summary>
internal sealed class RabbitMqReceiveContext(EventEnvelope envelope, IChannel channel, ulong deliveryTag) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
        // Native DLX moves the message; the SDK dead-letter store path captures the reason and exception.
        => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken);
}
