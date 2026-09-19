using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Drives the registered <see cref="IReceiveTransport"/>, routing each received message through the <see cref="MessagePump"/> into the <see cref="EventProcessingPipeline"/>. Transport-agnostic — in-memory and every broker adapter reuse it.</summary>
internal sealed class TransportConsumerBackgroundService(IReceiveTransport receiveTransport, MessagePump pump, BusControl busControl) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Mark the bus during the awaited host-start lifecycle rather than scheduled execution.
        await base.StartAsync(cancellationToken);

        // Mark running only after transport startup succeeds and unless an explicit stop already won.
        busControl.MarkRunning();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => receiveTransport.StartAsync(pump.DispatchAsync, stoppingToken).AsTask();

    /// <inheritdoc />
    /// <remarks>
    ///   - the stop order is load-bearing: cancel and drain the consume loop, await in-flight handlers, release the transport last
    ///   - releasing earlier disposes the channel a worker is about to ack on, and a poll-loop transport would be disposed mid-consume
    ///   - a paused bus stops on this path: the stopping token releases the consume loop parked at the pump's gate
    /// </remarks>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await pump.DrainAsync(cancellationToken);
        await receiveTransport.StopAsync(cancellationToken);
        busControl.MarkStopped();
    }
}
