using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Counts activity invocations across the whole host — saga state cannot, because a replay reloads it.</summary>
public sealed class SagaProbe
{
    private int _paymentActivityRuns;

    /// <summary>How many times the <see cref="PaymentReceived"/> transition's activities ran.</summary>
    public int PaymentActivityRuns => Volatile.Read(ref _paymentActivityRuns);

    /// <summary>Record one invocation.</summary>
    public void RecordPaymentActivity() => Interlocked.Increment(ref _paymentActivityRuns);
}
