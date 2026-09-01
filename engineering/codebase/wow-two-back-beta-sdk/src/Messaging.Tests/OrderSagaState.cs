using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Per-instance persisted state. <c>Interference</c> is written by a simulated concurrent writer, never by the machine.</summary>
public sealed class OrderSagaState : SagaState
{
    /// <summary>Copied off the initiating event — what a mis-correlated second event would visibly cross-contaminate.</summary>
    public decimal Total { get; set; }

    /// <summary>Bumped only by <see cref="ConflictOnceSagaRepository"/>, standing in for another process's committed write.</summary>
    public int Interference { get; set; }
}
