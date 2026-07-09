using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Drives the registered <see cref="IReceiveTransport"/>, routing each received message into the <see cref="EventProcessingPipeline"/>. Transport-agnostic — in-memory and every broker adapter reuse it.</summary>
internal sealed class TransportConsumerHostedService(IReceiveTransport receiveTransport, EventProcessingPipeline pipeline) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => receiveTransport.StartAsync((context, cancellationToken) => pipeline.ProcessAsync(context, cancellationToken), stoppingToken).AsTask();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Cancel + drain the consume loop FIRST (base awaits ExecuteAsync → StartAsync), then release the transport —
        // otherwise a poll-loop transport (Kafka) has its native consumer disposed mid-Consume and crashes.
        await base.StopAsync(cancellationToken);
        await receiveTransport.StopAsync(cancellationToken);
    }
}
