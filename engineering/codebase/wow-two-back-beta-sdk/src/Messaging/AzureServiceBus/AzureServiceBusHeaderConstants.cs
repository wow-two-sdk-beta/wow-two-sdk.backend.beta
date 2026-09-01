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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>Well-known Service Bus application-property keys the adapter sets.</summary>
/// <remarks>
///   - message id, correlation id, reply-to, content type, TTL, scheduled enqueue time and session id ride native properties
///   - a header carries only the metadata AMQP has no property for
/// </remarks>
internal static class AzureServiceBusHeaderConstants
{
    /// <summary>Carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = MessageHeaderConstants.EventType;

    /// <summary>Carries the serializer content type. Mirrored onto the native <see cref="ServiceBusMessage.ContentType"/>; the header is what a bridged non-SDK consumer reads.</summary>
    public const string ContentType = MessageHeaderConstants.ContentType;

    /// <summary>Carries the ordering / partition key, so it survives on a non-session entity where <c>SessionId</c> is ignored.</summary>
    public const string PartitionKey = MessageHeaderConstants.PartitionKey;

    /// <summary>Carries <see cref="EventEnvelope.ConversationId"/> — Service Bus has no property for it, and <c>CorrelationId</c> is already spoken for by the business flow.</summary>
    public const string ConversationId = MessageHeaderConstants.ConversationId;
}
