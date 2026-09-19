using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Refers to how a received message left the consume pipeline — the <c>messaging.consume.outcome</c> tag on the consumed counter.</summary>
public enum ConsumeOutcome
{
    /// <summary>The message reached its handler and processing completed.</summary>
    Success,

    /// <summary>Processing was exhausted (retries included); the message was dead-lettered.</summary>
    Faulted,

    /// <summary>The inbox had already processed this message id, so dispatch was skipped.</summary>
    Duplicate,

    /// <summary>No handler is registered for the event type; the message was settled without dispatch.</summary>
    NoHandler,

    /// <summary>
    /// The attempt threw, the registered <c>IEventFaultPolicy</c> returned <c>FaultDisposition.Ignore</c>, and the
    /// resilience pipeline swallowed the fault — so the message was acknowledged without any attempt completing.
    /// Distinct from <see cref="Success"/> (the handler never finished) and from <see cref="Faulted"/> (nothing was
    /// dead-lettered); an ignored message would otherwise leave the pipeline with no consumed count at all.
    /// </summary>
    Ignored,
}
