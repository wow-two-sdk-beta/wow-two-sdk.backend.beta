namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>
/// The outcome of an application operation — a closed discriminated union that either succeeds with a
/// typed <typeparamref name="TSuccess"/> payload or fails with a typed <typeparamref name="TFailure"/> error,
/// each carrying an optional, separately-typed context. The private constructor makes
/// <see cref="Success"/> and <see cref="Failure"/> the only possible cases, so the success/failure split is
/// compiler-checked at every call site. Collapse it with <c>Match</c> (see <c>AppResultExtensions</c>).
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
}
