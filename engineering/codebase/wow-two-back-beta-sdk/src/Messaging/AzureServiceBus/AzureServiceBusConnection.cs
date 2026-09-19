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

/// <summary>Lazily-opened shared <see cref="ServiceBusClient"/> (singleton) — the send and receive halves share one AMQP connection, as the client is designed for.</summary>
internal sealed class AzureServiceBusConnection(AzureServiceBusOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceBusClient? _client;

    public async ValueTask<ServiceBusClient> GetClientAsync(CancellationToken cancellationToken)
    {
        // The client reconnects its own AMQP links and retries transient faults, so only a disposed one is replaced.
        if (_client is not null)
            return _client;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null)
                return _client;

            var opt = options;
            ArgumentException.ThrowIfNullOrWhiteSpace(opt.ConnectionString, nameof(AzureServiceBusOptions.ConnectionString));

            _client = new ServiceBusClient(opt.ConnectionString, new ServiceBusClientOptions
            {
                TransportType = opt.UseWebSockets ? ServiceBusTransportType.AmqpWebSockets : ServiceBusTransportType.AmqpTcp,
            });

            return _client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
        _gate.Dispose();
    }
}
