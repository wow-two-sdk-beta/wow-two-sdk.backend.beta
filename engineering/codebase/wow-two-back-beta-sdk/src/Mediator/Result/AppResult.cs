using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Represents the outcome of an application operation — a typed success or an <see cref="AppError"/> failure, each with optional context.</summary>
/// <typeparam name="TSuccess">The success payload type.</typeparam>
public abstract record AppResult<TSuccess> where TSuccess : notnull
{
    private AppResult()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Creates a successful result carrying <paramref name="data"/>.</summary>
    /// <param name="data">The typed success payload.</param>
    /// <param name="context">Optional success-side context.</param>
    public static AppResult<TSuccess> Ok(TSuccess data, IAppSuccessContext? context = null)
    {
        return new Success { Data = data, Context = context };
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <param name="context">Optional failure-side context.</param>
    public static AppResult<TSuccess> Fail(AppError error, IAppFailureContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Failure { Error = error, Context = context };
    }

    /// <summary>Represents a successful operation carrying its data and optional context.</summary>
    public sealed record Success : AppResult<TSuccess>
    {
        /// <summary>Gets the typed success payload.</summary>
        public required TSuccess Data { get; init; }

        /// <summary>Gets the optional success-side context.</summary>
        public IAppSuccessContext? Context { get; init; }
    }

    /// <summary>Represents a failed operation carrying its error and optional context.</summary>
    public sealed record Failure : AppResult<TSuccess>
    {
        /// <summary>Gets the error describing the failure.</summary>
        public required AppError Error { get; init; }

        /// <summary>Gets the optional failure-side context.</summary>
        public IAppFailureContext? Context { get; init; }
    }

    /// <summary>Collapses the union by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The unified return type.</typeparam>
    /// <param name="onSuccess">Invoked with the success case.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    public TOut Match<TOut>(Func<Success, TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success success => onSuccess(success),
            Failure failure => onFailure(failure),
            _ => throw new InvalidOperationException("AppResult is a closed union."),
        };
    }

    /// <summary>Collapses the union with a no-argument success arm — for void-ish commands whose success payload is irrelevant.</summary>
    /// <typeparam name="TOut">The unified return type.</typeparam>
    /// <param name="onSuccess">Invoked with no argument on success.</param>
    /// <param name="onFailure">Invoked with the failure case.</param>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success => onSuccess(),
            Failure failure => onFailure(failure),
            _ => throw new InvalidOperationException("AppResult is a closed union."),
        };
    }
}
