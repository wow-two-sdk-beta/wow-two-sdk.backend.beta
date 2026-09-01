using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// The consume filter that applies <see cref="SecondLevelRetryOptions"/> — catches the fault that escaped the
/// first-level retry loop and, where a tier is left, re-publishes the message on a delay and swallows the fault so the
/// pipeline settles this delivery.
/// </summary>
/// <remarks>
///   - an exception reaching it has already exhausted the in-process retry budget
///   - registration order is filter order
///   - register after the application's own filters to keep it innermost
/// </remarks>
internal sealed class RetryingConsumeInterceptor(SecondLevelRetryCoordinator coordinator) : IConsumeInterceptor
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Inactive installs no try/catch, so the fault propagates straight to the pipeline's dead-letter path.
        if (!coordinator.IsActive)
        {
            await next(context, cancellationToken);
            return;
        }

        try
        {
            await next(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!await coordinator.TryPromoteAsync(context, exception, cancellationToken))
                throw;

            // Swallowed on purpose: the message is back on the wire with a future delivery time, so the pipeline acknowledges.
        }
    }
}
