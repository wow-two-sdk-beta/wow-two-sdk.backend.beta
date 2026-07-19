using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Drives the registered <see cref="IReceiveTransport"/>, routing each received message through the <see cref="MessagePump"/> into the <see cref="EventProcessingPipeline"/>. Transport-agnostic — in-memory and every broker adapter reuse it.</summary>
internal sealed class TransportConsumerHostedService(IReceiveTransport receiveTransport, MessagePump pump, BusControl busControl) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // The host lifecycle is what moves IBusControl off Stopped, and the stamp belongs HERE rather than in
        // ExecuteAsync: IHostedService.StartAsync is awaited by the host, ExecuteAsync is not. On .NET 10 BackgroundService
        // schedules ExecuteAsync on the thread pool and returns, so stamping there left a window — sometimes milliseconds
        // wide under a saturated pool — in which IHost.StartAsync had returned and IBusControl still reported Stopped.
        // A caller that paused or queried the bus straight after startup saw a bus that had never run.
        await base.StartAsync(cancellationToken);

        // After the base call, so a transport that fails to start leaves the bus reporting Stopped rather than a Running
        // it never reached. MarkRunning stands down if the bus was already stopped explicitly, so a kill-switch pulled
        // before startup is not undone by the host starting.
        busControl.MarkRunning();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => receiveTransport.StartAsync(pump.DispatchAsync, stoppingToken).AsTask();

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
