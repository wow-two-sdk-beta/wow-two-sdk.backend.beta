namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Represents the outcome of an application operation — a closed union of <see cref="Success"/> (typed <typeparamref name="TSuccess"/>) or <see cref="Failure"/> (typed <typeparamref name="TFailure"/>), each with optional context.</summary>
/// <typeparam name="TSuccess">Success payload type — an <see cref="ISuccessResult"/>.</typeparam>
/// <typeparam name="TFailure">Failure payload type — an <see cref="IFailureResult"/>.</typeparam>
public abstract record AppResult<TSuccess, TFailure>
    where TSuccess : ISuccessResult
    where TFailure : IFailureResult
{
    private AppResult() { }

    /// <summary>A successful operation carrying its <paramref name="Data"/> and optional <paramref name="Context"/>.</summary>
    /// <param name="Data">The typed success payload.</param>
    /// <param name="Context">Optional success-side context (e.g. cache-hit metadata, pagination info).</param>
    public sealed record Success(TSuccess Data, IApplicationSuccessContext? Context = null) : AppResult<TSuccess, TFailure>;

    /// <summary>A failed operation carrying its <paramref name="Error"/> and optional <paramref name="Context"/>.</summary>
    /// <param name="Error">The typed failure payload.</param>
    /// <param name="Context">Optional failure-side context (e.g. retry-after hints, validation detail).</param>
    public sealed record Failure(TFailure Error, IApplicationFailureContext? Context = null) : AppResult<TSuccess, TFailure>;

    /// <summary>Collapses the union to a single <typeparamref name="TOut"/> — <paramref name="onSuccess"/> receives the <see cref="Success"/> case, <paramref name="onFailure"/> the <see cref="Failure"/> case.</summary>
    /// <typeparam name="TOut">Unified return type both arms produce.</typeparam>
    /// <param name="onSuccess">Invoked with the success case.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    /// <returns>The value produced by whichever arm ran.</returns>
    public TOut Match<TOut>(Func<Success, TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success s => onSuccess(s),
            Failure f => onFailure(f),
            _ => throw new InvalidOperationException("Unreachable — AppResult is a closed union."),
        };
    }

    /// <summary>Collapses the union to a single <typeparamref name="TOut"/> with a no-argument success arm — for void-ish commands whose success payload is irrelevant.</summary>
    /// <typeparam name="TOut">Unified return type both arms produce.</typeparam>
    /// <param name="onSuccess">Invoked with no argument on success.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    /// <returns>The value produced by whichever arm ran.</returns>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success => onSuccess(),
            Failure f => onFailure(f),
            _ => throw new InvalidOperationException("Unreachable — AppResult is a closed union."),
        };
    }
}
