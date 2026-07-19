using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Drives the registered <see cref="IReceiveTransport"/>, routing each received message through the <see cref="MessagePump"/> into the <see cref="EventProcessingPipeline"/>. Transport-agnostic — in-memory and every broker adapter reuse it.</summary>
internal sealed class TransportConsumerHostedService(IReceiveTransport receiveTransport, MessagePump pump, BusControl busControl) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The host lifecycle is what moves IBusControl off Stopped. MarkRunning stands down if the bus was already
        // stopped explicitly, so a kill-switch pulled before startup is not undone by the host starting.
        busControl.MarkRunning();
        return receiveTransport.StartAsync(pump.DispatchAsync, stoppingToken).AsTask();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Order is load-bearing. Cancel + drain the consume loop FIRST (base awaits ExecuteAsync → StartAsync) so no new
        // messages arrive; then await handlers still in flight on pump workers, which settle through the transport; only
        // then release the transport. Releasing earlier disposes the channel a worker is about to ack on, and a poll-loop
        // transport (Kafka) would be disposed mid-Consume.
        // A paused bus stops on exactly this path: base.StopAsync cancels the stopping token, which releases the consume
        // loop parked at the pump's gate with an OperationCanceledException every transport already treats as shutdown.
        await base.StopAsync(cancellationToken);
        await pump.DrainAsync(cancellationToken);
        await receiveTransport.StopAsync(cancellationToken);
        busControl.MarkStopped();
    }
}
