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

/// <summary>Idempotent consumer-group provisioning.</summary>
internal sealed class RedisStreamsTopologyBroker
{
    /// <summary>Redis' "the group already exists" error, which is the normal answer on every start after the first.</summary>
    private const string GroupExistsPrefix = "BUSYGROUP";

    /// <summary>Redis' "no such key or consumer group" error, raised by a read after the key was dropped underneath the loop.</summary>
    public const string GroupMissingPrefix = "NOGROUP";

    /// <summary>Create the consumer group on every stream this process reads. Safe to repeat and safe to race.</summary>
    /// <param name="database">The Redis database.</param>
    /// <param name="streams">The stream keys to provision.</param>
    /// <param name="consumerGroup">The consumer group name.</param>
    public async ValueTask EnsureGroupsAsync(IDatabase database, IReadOnlyList<string> streams, string consumerGroup)
    {
        foreach (var stream in streams)
        {
            try
            {
                // MKSTREAM creates the group before the first message; NewMessages ("$") starts it at the tail.
                await database.StreamCreateConsumerGroupAsync(stream, consumerGroup, StreamPosition.NewMessages, createStream: true);
            }
            catch (RedisServerException exception) when (exception.Message.StartsWith(GroupExistsPrefix, StringComparison.Ordinal))
            {
                // Group already provisioned, by an earlier run or by another instance racing this one.
            }
        }
    }
}
