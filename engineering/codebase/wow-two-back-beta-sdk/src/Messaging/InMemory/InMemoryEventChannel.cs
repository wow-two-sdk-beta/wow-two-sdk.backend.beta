using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Single in-process channel backing the in-memory transport.</summary>
internal sealed class InMemoryEventChannel
{
    private readonly Channel<EventEnvelope> _channel;

    public InMemoryEventChannel(InMemoryEventBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var capacity = options.ChannelCapacity;
        _channel = capacity > 0
            ? Channel.CreateBounded<EventEnvelope>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            })
            : Channel.CreateUnbounded<EventEnvelope>();
    }

    public ChannelReader<EventEnvelope> Reader => _channel.Reader;

    public ChannelWriter<EventEnvelope> Writer => _channel.Writer;
}
