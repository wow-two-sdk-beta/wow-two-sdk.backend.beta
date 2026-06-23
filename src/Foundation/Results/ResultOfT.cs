using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Represents the outcome of an operation that returns a <typeparamref name="T"/> value.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public abstract record Result<T> where T : notnull
{
    private Result()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Represents a successful operation carrying its value.</summary>
    public sealed record Success : Result<T>
    {
        /// <summary>Gets the value produced by the operation.</summary>
        public required T Value { get; init; }
    }

    /// <summary>Represents a failed operation carrying its error.</summary>
    public sealed record Failure : Result<T>
    {
        /// <summary>Gets the error describing the failure.</summary>
        public required AppError Error { get; init; }
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to return.</param>
    public static Result<T> Ok(T value)
    {
        return new Success { Value = value };
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result<T> Fail(AppError error)
    {
        return new Failure { Error = error };
    }

    /// <summary>Collapses the result by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the error on failure.</param>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success success => onSuccess(success.Value),
            Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Result<T> has only Success and Failure cases."),
        };
    }

    /// <summary>Transforms the success value, propagating a failure unchanged.</summary>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="selector">Projects the success value into the new value.</param>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector) where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Match(value => Result<TOut>.Ok(selector(value)), Result<TOut>.Fail);
    }
}
