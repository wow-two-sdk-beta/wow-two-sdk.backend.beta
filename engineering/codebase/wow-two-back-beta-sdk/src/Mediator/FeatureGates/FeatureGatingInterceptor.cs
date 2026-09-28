using WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.FeatureGates;

/// <summary>
/// Refuses an <see cref="IFeatureGated"/> request as not found while any of its features is disabled, so a dark
/// feature behaves as if it did not exist; other requests pass straight through.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="flags">Evaluates feature flags.</param>
public sealed class FeatureGatingInterceptor<TRequest, TResponse>(IFeatureFlags flags) : IRequestInterceptor<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);

        if (request is IFeatureGated gated)
        {
            foreach (var feature in gated.RequiredFeatures)
            {
                if (!await flags.IsEnabledAsync(feature, cancellationToken).ConfigureAwait(false))
                    throw AppErrorFactory.NotFound("The requested operation is not available.").ToException();
            }
        }

        return await nextStep().ConfigureAwait(false);
    }
}
