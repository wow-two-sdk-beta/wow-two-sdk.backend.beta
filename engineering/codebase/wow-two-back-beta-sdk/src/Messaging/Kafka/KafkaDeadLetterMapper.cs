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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;

/// <summary>Builds a dead-letter Kafka message — preserves the original key/value/headers and stamps death headers (reason + exception type) for triage.</summary>
internal static class KafkaDeadLetterMapper
{
    public static Message<string, byte[]> Build(Message<string, byte[]> original, string reason, Exception? exception)
    {
        var headers = new Headers();
        if (original.Headers is not null)
            foreach (var header in original.Headers)
                headers.Add(header.Key, header.GetValueBytes());
        headers.Add(MessageHeaderConstants.DeadLetterReason, Encoding.UTF8.GetBytes(reason));
        if (exception is not null)
            headers.Add(MessageHeaderConstants.DeadLetterExceptionType, Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name));
        return new Message<string, byte[]> { Key = original.Key, Value = original.Value, Headers = headers };
    }
}
