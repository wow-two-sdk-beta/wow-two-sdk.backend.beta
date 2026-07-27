namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>The next step in the consume pipeline — call it to continue, or skip it to short-circuit (drop/handle the message).</summary>
/// <param name="context">The receive context.</param>
/// <param name="cancellationToken">Cancellation token.</param>
public delegate ValueTask ConsumeDelegate(ReceiveContext context, CancellationToken cancellationToken);

/// <summary>
/// A pluggable middleware around message consumption. Filters run <b>once per message</b>, ordered by registration,
/// wrapping the resilience → dedupe → dispatch core (so they see the whole outcome, outside the retry loop). Use for
/// fault-publishing, wire-tap/audit, claim-check rehydrate, enrichment, rate-limiting, and similar cross-cutting concerns.
/// Register with <c>AddConsumeFilter&lt;T&gt;()</c>.
/// </summary>
public interface IConsumeFilter
{
    /// <summary>Process the message; call <paramref name="next"/> to continue the chain, or return without calling it to short-circuit.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="next">The next step in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken);
}
