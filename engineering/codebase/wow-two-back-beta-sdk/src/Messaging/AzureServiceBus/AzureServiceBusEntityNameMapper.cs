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

/// <summary>
/// Sanitizes a topology routing key into a legal Service Bus entity or rule name. Service Bus accepts letters, digits,
/// periods, hyphens and underscores, and caps a subscription or rule name at 50 characters — far shorter than the
/// namespace-qualified type tokens the topology produces, so a long name is truncated and disambiguated by a hash of
/// the original rather than silently colliding with its siblings.
/// </summary>
internal static class AzureServiceBusEntityNameMapper
{
    /// <summary>Service Bus ceiling on a subscription or rule name.</summary>
    public const int MaxSubscriptionLength = 50;

    /// <summary>Service Bus ceiling on a topic (or queue) name.</summary>
    public const int MaxTopicLength = 260;

    /// <summary>Characters reserved for the hash suffix on a truncated name: a separator plus eight hex digits.</summary>
    private const int HashSuffixLength = 9;

    /// <summary>The legal name for <paramref name="value"/>, truncated with a stable hash suffix when it exceeds <paramref name="maxLength"/>.</summary>
    /// <param name="value">A routing key, endpoint name, or other candidate name.</param>
    /// <param name="maxLength">The Service Bus ceiling for the entity kind being named.</param>
    public static string Sanitize(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            // The management API rejects non-ASCII letters; every other character collapses to '-'.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        // A name may not start or end with a separator; trimming can empty the string, which is not a name at all.
        var name = builder.ToString().Trim('.', '-', '_');
        if (name.Length == 0)
            name = "wt";

        if (name.Length <= maxLength)
            return name;

        // The hash is over the full name, so two names sharing a truncated prefix keep distinct rules across restarts.
        var hash = StableHash(name).ToString("x8", CultureInfo.InvariantCulture);
        // Span TrimEnd takes a set as one span, not params chars.
        return string.Concat(name.AsSpan(0, maxLength - HashSuffixLength).TrimEnd(".-_".AsSpan()), "-", hash);
    }

    // FNV-1a is stable across processes; a randomised hash orphans last deploy's rule and adds a second beside it.
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash;
    }
}
