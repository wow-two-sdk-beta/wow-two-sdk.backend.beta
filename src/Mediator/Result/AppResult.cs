namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>
/// The outcome of an application operation — a closed discriminated union that either succeeds with a
/// typed <typeparamref name="TSuccess"/> payload or fails with a typed <typeparamref name="TFailure"/> error,
/// each carrying an optional, separately-typed context. The private constructor makes
/// <see cref="Success"/> and <see cref="Failure"/> the only possible cases, so the success/failure split is
/// compiler-checked at every call site. Collapse it with <see cref="Match{TOut}(System.Func{Success,TOut},System.Func{Failure,TOut})"/>.
/// </summary>
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

    /// <summary>
    /// Collapse the union to a single <typeparamref name="TOut"/> — the success arm receives the
    /// <see cref="Success"/> case (reach <c>.Data</c> / <c>.Context</c>), the failure arm receives the
    /// <see cref="Failure"/> case (reach <c>.Error</c> / <c>.Context</c>). The mandated consume path so call
    /// sites (controllers) stop hand-rolling <c>switch</c>/<c>is</c> over the cases. Only <typeparamref name="TOut"/>
    /// is explicit — <c>TSuccess</c>/<c>TFailure</c> come from the instance.
    /// </summary>
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

    /// <summary>
    /// Collapse the union to a single <typeparamref name="TOut"/> with a no-argument success arm — for
    /// void-ish commands where the success payload is irrelevant and the controller maps it straight to a
    /// body-less result (e.g. <c>NoContent()</c>). The failure arm still receives the <see cref="Failure"/> case.
    /// </summary>
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
