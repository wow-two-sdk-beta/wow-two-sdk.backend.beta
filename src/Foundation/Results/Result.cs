using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Represents the outcome of an operation that returns no value.</summary>
public abstract record Result
{
    private Result()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Represents a successful operation.</summary>
    public sealed record Success : Result;

    /// <summary>Represents a failed operation carrying its error.</summary>
    public sealed record Failure : Result
    {
        /// <summary>Gets the error describing the failure.</summary>
        public required AppError Error { get; init; }
    }

    /// <summary>Creates a successful result.</summary>
    public static Result Ok()
    {
        return new Success();
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result Fail(AppError error)
    {
        return new Failure { Error = error };
    }

    /// <summary>Collapses the result by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success => onSuccess(),
            Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Result has only Success and Failure cases."),
        };
    }

    /// <summary>Lifts the outcome into a value-carrying result, running <paramref name="selector"/> on success and propagating a failure unchanged.</summary>
    /// <typeparam name="TOut">The value type to lift into.</typeparam>
    /// <param name="selector">Produces the success value when the operation succeeded.</param>
    public Result<TOut> Map<TOut>(Func<TOut> selector) where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Match(() => Result<TOut>.Ok(selector()), Result<TOut>.Fail);
    }
}
