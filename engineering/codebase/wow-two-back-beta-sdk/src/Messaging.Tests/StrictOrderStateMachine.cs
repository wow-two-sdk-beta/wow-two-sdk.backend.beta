using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// Same flow as <see cref="OrderStateMachine"/>, but <see cref="PaymentReceived"/> declares
/// <see cref="SagaMissingInstance.Fault"/> — a payment for an order nobody placed means the events arrived out of
/// order, not that the flow already finished.
/// </summary>
public sealed class StrictOrderStateMachine : SagaStateMachine<StrictOrderSagaState>
{
    /// <summary>Holds the one intermediate state.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>Declare the behaviour.</summary>
    public StrictOrderStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .IfMissing(SagaMissingInstance.Fault)
                .Finalize());
    }
}
