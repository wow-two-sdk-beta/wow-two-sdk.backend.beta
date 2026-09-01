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

/// <summary>
/// Maps a topology routing key onto a legal Kafka topic name. Kafka accepts <c>[A-Za-z0-9._-]</c> up to 249
/// characters and rejects <c>.</c> / <c>..</c> outright, so a key a custom <see cref="ITopologyService"/> returns
/// is sanitized before it becomes a topic.
/// </summary>
/// <remarks>
///   - send and receive paths map the same keys, so a produced topic is always one the consumer subscribes to
/// </remarks>
internal static class KafkaTopicNameMapper
{
    /// <summary>Kafka's own ceiling on a topic name.</summary>
    private const int MaxLength = 249;

    /// <summary>The topic for <paramref name="routingKey"/>, or null when nothing addressable survives sanitizing.</summary>
    public static string? From(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(Math.Min(routingKey.Length, MaxLength));
        foreach (var character in routingKey)
        {
            if (builder.Length == MaxLength)
                break;

            // Kafka rejects non-ASCII letters, so anything outside the legal set collapses to '-'.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        var topic = builder.ToString().Trim('.');
        return topic.Length == 0 ? null : topic;
    }
}
