namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>
/// Collapse helpers for <see cref="AppResult{TSuccess,TFailure}"/>. Kept as extensions so the union stays a
/// pure data record. <c>Match</c> turns the closed union into a single output value — the mandated consume
/// path so call sites (controllers) stop hand-rolling <c>switch</c>/<c>is</c> over the cases.
/// </summary>
public static class AppResultExtensions
{
    /// <summary>
    /// Collapse the union to a single <typeparamref name="TOut"/> — the success arm receives the
    /// <see cref="AppResult{TSuccess,TFailure}.Success"/> case (reach <c>.Data</c> / <c>.Context</c>),
    /// the failure arm receives the <see cref="AppResult{TSuccess,TFailure}.Failure"/> case
    /// (reach <c>.Error</c> / <c>.Context</c>).
    /// </summary>
    /// <typeparam name="TSuccess">Success payload type.</typeparam>
    /// <typeparam name="TFailure">Failure payload type.</typeparam>
    /// <typeparam name="TOut">Unified return type both arms produce.</typeparam>
    /// <param name="result">The union to collapse.</param>
    /// <param name="onSuccess">Invoked with the success case.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    public static TOut Match<TSuccess, TFailure, TOut>(
        this AppResult<TSuccess, TFailure> result,
        Func<AppResult<TSuccess, TFailure>.Success, TOut> onSuccess,
        Func<AppResult<TSuccess, TFailure>.Failure, TOut> onFailure)
        where TSuccess : ISuccessResult
        where TFailure : IFailureResult
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result switch
        {
            AppResult<TSuccess, TFailure>.Success s => onSuccess(s),
            AppResult<TSuccess, TFailure>.Failure f => onFailure(f),
            _ => throw new InvalidOperationException("Unreachable — AppResult is a closed union."),
        };
    }

    /// <summary>
    /// Collapse the union to a single <typeparamref name="TOut"/> with a no-argument success arm — for
    /// void-ish commands where the success payload is irrelevant and the controller maps it straight to a
    /// body-less result (e.g. <c>NoContent()</c>). The failure arm still receives the
    /// <see cref="AppResult{TSuccess,TFailure}.Failure"/> case.
    /// </summary>
    /// <typeparam name="TSuccess">Success payload type.</typeparam>
    /// <typeparam name="TFailure">Failure payload type.</typeparam>
    /// <typeparam name="TOut">Unified return type both arms produce.</typeparam>
    /// <param name="result">The union to collapse.</param>
    /// <param name="onSuccess">Invoked with no argument on success.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    public static TOut Match<TSuccess, TFailure, TOut>(
        this AppResult<TSuccess, TFailure> result,
        Func<TOut> onSuccess,
        Func<AppResult<TSuccess, TFailure>.Failure, TOut> onFailure)
        where TSuccess : ISuccessResult
        where TFailure : IFailureResult
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result switch
        {
            AppResult<TSuccess, TFailure>.Success => onSuccess(),
            AppResult<TSuccess, TFailure>.Failure f => onFailure(f),
            _ => throw new InvalidOperationException("Unreachable — AppResult is a closed union."),
        };
    }
}
