using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>Typed pending load — holds the from-state until a write pairs with it.</summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class SagaPendingWrite<TState> : ISagaPendingWrite
    where TState : class, ISagaState
{
    public required SagaRecorder<TState> Recorder { get; init; }

    public required string CorrelationId { get; init; }

    public required string? FromState { get; init; }

    public required TState? Loaded { get; init; }

    public required int LoadedVersion { get; init; }

    public required EventEnvelope? Envelope { get; init; }

    public int Attempt { get; set; }

    public void Flush(SagaTransitionOutcome outcome, Exception? exception)
        => Recorder.Append(new RecordedTransition<TState>
        {
            CorrelationId = CorrelationId,
            FromState = FromState,
            ToState = FromState, // nothing was written, so the instance is where it was
            Outcome = outcome,
            Attempt = Attempt,
            Envelope = Envelope,
            Instance = Loaded,
            Version = LoadedVersion,
            Exception = exception,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        });
}
