using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The machine under test: initiate → schedule a timeout → advance on payment, or cancel on the timeout.</summary>
public sealed class OrderStateMachine : SagaStateMachine<OrderSagaState>
{
    /// <summary>Holds the one intermediate state.</summary>
    public const string AwaitingPayment = "awaiting-payment";

    /// <summary>Holds how long the instance waits for payment before the scheduled timeout fires.</summary>
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(30);

    /// <summary>Declare the behaviour.</summary>
    public OrderStateMachine()
    {
        Initially(
            When<OrderPlaced>(e => e.OrderId)
                .Then(context => context.Saga.Total = context.Message.Total)
                .Schedule(PaymentWindow, context => new PaymentOverdue { OrderId = context.CorrelationId })
                .TransitionTo(AwaitingPayment));

        During(
            AwaitingPayment,
            When<PaymentReceived>(e => e.OrderId)
                .Unschedule<PaymentOverdue>()
                .Then(context => context.Services.GetService<SagaProbe>()?.RecordPaymentActivity())
                .Publish(context => new OrderConfirmed { OrderId = context.CorrelationId, Total = context.Saga.Total + context.Message.Amount })
                .Finalize(),
            When<PaymentOverdue>()
                .Publish(context => new OrderCancelled { OrderId = context.CorrelationId })
                .Finalize());
    }
}
