using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>Maps a topology routing key onto a stream key nested under the adapter's configured root stream.</summary>
/// <remarks>
///   - <c>SCAN MATCH wt.events*</c> finds one deployment's streams
///   - one root hash tag covers the whole set
///   - both halves of the adapter map the same keys, so the send path's stream is one the consume loop reads
/// </remarks>
internal static class RedisStreamNameMapper
{
    /// <summary>Ceiling on a generated key. Redis itself allows 512 MB keys; this keeps a routing key from producing something no human can read in <c>SCAN</c> output.</summary>
    private const int MaxLength = 512;

    /// <summary>The stream key for <paramref name="routingKey"/> under <paramref name="root"/>.</summary>
    /// <param name="root">The configured root stream (<see cref="RedisStreamsOptions.Stream"/>).</param>
    /// <param name="routingKey">A topology routing key, or an address.</param>
    public static string Route(string root, string? routingKey)
    {
        var token = Sanitize(routingKey);
        if (token is null)
            return root;

        // An already-rooted destination is not prefixed again, so a re-add lands back on the same key.
        if (IsRootedAt(token, root))
            return token;

        var routed = string.Concat(root, ".", token);
        return routed.Length <= MaxLength ? routed : routed[..MaxLength];
    }

    /// <summary>True when <paramref name="key"/> is <paramref name="root"/> itself or sits beneath it.</summary>
    /// <param name="key">The key to test.</param>
    /// <param name="root">The root stream key.</param>
    public static bool IsRootedAt(string key, string root)
        => string.Equals(key, root, StringComparison.Ordinal)
            || (key.Length > root.Length && key.StartsWith(root, StringComparison.Ordinal) && key[root.Length] == '.');

    /// <summary>
    /// A key-safe token, or null when nothing addressable survives. Every key Redis accepts is legal on the wire, so
    /// this guards operability rather than validity: <c>{</c> and <c>}</c> above all, which Redis Cluster reads as a
    /// hash tag and which would silently move one routed stream to a different slot than its siblings — the shape that
    /// breaks a multi-stream read. Glob metacharacters go too, so <c>SCAN MATCH</c> over the set behaves.
    /// </summary>
    private static string? Sanitize(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(Math.Min(routingKey.Length, MaxLength));
        foreach (var character in routingKey)
        {
            if (builder.Length == MaxLength)
                break;

            // Accept ASCII alphanumerics; everything else collapses to '-'.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        var key = builder.ToString().Trim('.');
        return key.Length == 0 ? null : key;
    }
}
