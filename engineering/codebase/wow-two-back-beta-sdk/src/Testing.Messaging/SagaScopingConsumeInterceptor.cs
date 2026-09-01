using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// Pass-through consume filter that opens a <see cref="SagaConsumeBracket"/> around the message. A filter, not an
/// observer: an observer's hooks return before the handler runs, so an ambient it sets never reaches the coordinator,
/// while a filter wraps the whole dispatch on one async flow.
/// </summary>
internal sealed class SagaScopingConsumeInterceptor : IConsumeInterceptor
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (SagaConsumeBracket.Current is not null)
        {
            await next(context, cancellationToken); // already bracketed — nesting would shadow the outer envelope
            return;
        }

        var bracket = new SagaConsumeBracket(context.Envelope);
        SagaConsumeBracket.Current = bracket;
        try
        {
            await next(context, cancellationToken);
            bracket.FlushPending(SagaTransitionOutcome.Ignored, exception: null);
        }
        catch (Exception exception)
        {
            // The instance never moved; rethrown untouched, so settlement is unchanged.
            bracket.FlushPending(SagaTransitionOutcome.Faulted, exception);
            throw;
        }
        finally
        {
            SagaConsumeBracket.Current = null;
        }
    }
}
