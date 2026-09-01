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
/// Maps a topology routing key onto a NATS subject nested under the adapter's configured root subject.
/// </summary>
/// <remarks>
///   - nests the key under the root, so one <c>{root}.&gt;</c> stream subject accepts any address
///   - reaches an endpoint owned by another service through that same wildcard
///   - send and consume paths map identical keys, so a published subject is always a filtered one
/// </remarks>
internal static class NatsSubjectNameMapper
{
    /// <summary>The subject for <paramref name="routingKey"/> under <paramref name="root"/>.</summary>
    /// <param name="root">The configured root subject (<see cref="NatsOptions.Subject"/>).</param>
    /// <param name="routingKey">A topology routing key, or an address.</param>
    public static string Route(string root, string? routingKey)
    {
        var token = Sanitize(routingKey);
        if (token is null)
            return root;

        // An already-rooted token is not prefixed again, so a re-publish lands back on the same subject.
        if (IsRootedAt(token, root))
            return token;

        return string.Concat(root, ".", token);
    }

    /// <summary>True when <paramref name="subject"/> is <paramref name="root"/> itself or sits beneath it.</summary>
    /// <param name="subject">The subject to test.</param>
    /// <param name="root">The root subject.</param>
    public static bool IsRootedAt(string subject, string root)
        => string.Equals(subject, root, StringComparison.Ordinal)
            || (subject.Length > root.Length && subject.StartsWith(root, StringComparison.Ordinal) && subject[root.Length] == '.');

    /// <summary>
    /// A legal subject token sequence, or null when nothing addressable survives. NATS separates tokens with <c>.</c>
    /// and reserves <c>*</c> and <c>&gt;</c> as wildcards — a key carrying either would silently widen the subscription
    /// — while an empty token (from <c>..</c>, or a leading <c>.</c>) is rejected outright by the server.
    /// </summary>
    private static string? Sanitize(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(routingKey.Length);
        var atTokenStart = true; // true also at index 0, so a leading '.' cannot open an empty token
        foreach (var character in routingKey)
        {
            if (character is '.')
            {
                if (!atTokenStart)
                {
                    builder.Append('.');
                    atTokenStart = true;
                }

                continue;
            }

            // ASCII only — anything outside the legal set collapses to '-', keeping distinct keys distinct.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_';
            builder.Append(legal ? character : '-');
            atTokenStart = false;
        }

        var subject = builder.ToString().TrimEnd('.');
        return subject.Length == 0 ? null : subject;
    }
}
