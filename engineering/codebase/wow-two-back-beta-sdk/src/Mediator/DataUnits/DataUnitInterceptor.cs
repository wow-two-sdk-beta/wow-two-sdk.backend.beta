using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.DataUnits;

/// <summary>Completes marked requests atomically, abandoning exceptions and failed result carriers.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The response contract.</typeparam>
/// <param name="session">The scoped transaction owner.</param>
public sealed class DataUnitInterceptor<TRequest, TResponse>(IDataSession session)
    : IRequestInterceptor<TRequest, TResponse> where TRequest : notnull
{
    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> nextStep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (request is not ITransactionalRequest)
        {
            return await nextStep().ConfigureAwait(false);
        }

        await using IDataUnit unit = await session.BeginAsync(cancellationToken).ConfigureAwait(false);
        TResponse response = await nextStep().ConfigureAwait(false);
        if (response is not IResult { IsSuccess: false })
        {
            await unit.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
        return response;
    }
}
