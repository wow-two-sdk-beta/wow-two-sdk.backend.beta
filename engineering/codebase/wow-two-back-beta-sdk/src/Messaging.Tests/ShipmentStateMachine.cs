using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// A flow that does not finalize on payment, so a cancelled timeout still has a live state to arrive into. That
/// is what makes the stale-token drop assertable: the <c>Paid</c> state declares a clause for
/// <see cref="PaymentOverdue"/>, so if the token check were skipped the timeout would cancel a paid order.
/// </summary>
public sealed class ShipmentStateMachine : SagaStateMachine<ShipmentSagaState>
{
    /// <summary>Holds waiting for the payment that was scheduled against a timeout.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>Holds paid — still running, and still bound to the timeout it cancelled.</summary>
    public const string Paid = "paid";

    /// <summary>Holds how long the instance waits for payment.</summary>
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);

    /// <summary>Declare the behaviour.</summary>
    public ShipmentStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .Schedule(PaymentWindow, context => new PaymentOverdue { OrderId = context.CorrelationId })
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .Unschedule<PaymentOverdue>()
                .TransitionTo(Paid),
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled { OrderId = context.CorrelationId })
                .Finalize());

        // The trap: a live clause for the cancelled timeout. Only the token check stops it firing.
        During(
            Paid,
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled { OrderId = context.CorrelationId })
                .Finalize());
    }
}
