namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>A timeout a transition asked for, held until the instance's new state is safely written.</summary>
internal sealed class SagaTimeoutRequest
{
    public required string CorrelationId { get; init; }

    public required string Name { get; init; }

    public required string Token { get; init; }

    public required TimeSpan Delay { get; init; }

    public required object Message { get; init; }

    public required Type MessageType { get; init; }

    /// <summary>Publishes the timeout with its own static type — captured at declaration, where the type is still known.</summary>
    public required Func<IEventBus, PublishOptions, CancellationToken, ValueTask> Publish { get; init; }
}
