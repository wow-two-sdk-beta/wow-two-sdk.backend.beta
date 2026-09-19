using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Single in-process channel backing the in-memory transport.</summary>
internal sealed class InMemoryEventChannel
{
    private readonly Channel<EventEnvelopeModel> _channel;

    public InMemoryEventChannel(InMemoryEventBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _channel = Channel.CreateBounded<EventEnvelopeModel>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public ChannelReader<EventEnvelopeModel> Reader => _channel.Reader;

    public ChannelWriter<EventEnvelopeModel> Writer => _channel.Writer;
}
