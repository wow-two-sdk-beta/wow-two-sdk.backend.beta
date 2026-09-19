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

/// <summary>Represents the outcome of an operation whose failure is one of a closed set the caller branches on.</summary>
/// <typeparam name="TSuccess">The success value type.</typeparam>
/// <typeparam name="TFailure">The failure case type — a domain enum or a sealed union, closed so a <see langword="switch"/> over it is exhaustive.</typeparam>
/// <remarks>The handler maps each failure case to its catalog <see cref="AppError"/> — <see cref="ToResult"/> for the default carrier, <see cref="Match{TOut}"/> for anything else; the carrier itself never learns a status.</remarks>
public abstract record Result<TSuccess, TFailure>
    where TSuccess : notnull
    where TFailure : notnull
{
    private Result()
    {
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Represents a successful operation carrying its value.</summary>
    public sealed record Success : Result<TSuccess, TFailure>
    {
        /// <summary>Gets the value produced by the operation.</summary>
        public required TSuccess Value { get; init; }
    }

    /// <summary>Represents a failed operation carrying which failure happened.</summary>
    public sealed record Failure : Result<TSuccess, TFailure>
    {
        /// <summary>Gets the failure case.</summary>
        public required TFailure Error { get; init; }
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to return.</param>
    public static Result<TSuccess, TFailure> Ok(TSuccess value)
    {
        return new Success { Value = value };
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The failure case.</param>
    public static Result<TSuccess, TFailure> Fail(TFailure error)
    {
        return new Failure { Error = error };
    }

    /// <summary>Collapses the result by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked with the value on success.</param>
    /// <param name="onFailure">Invoked with the failure case on failure.</param>
    public TOut Match<TOut>(Func<TSuccess, TOut> onSuccess, Func<TFailure, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return this switch
        {
            Success success => onSuccess(success.Value),
            Failure failure => onFailure(failure.Error),
            _ => throw new InvalidOperationException("Result<TSuccess, TFailure> has only Success and Failure cases."),
        };
    }

    /// <summary>Transforms the success value, propagating a failure unchanged.</summary>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="selector">Projects the success value into the new value.</param>
    public Result<TOut, TFailure> Map<TOut>(Func<TSuccess, TOut> selector) where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Match(value => Result<TOut, TFailure>.Ok(selector(value)), Result<TOut, TFailure>.Fail);
    }

    /// <summary>Maps up into the default carrier, translating the failure case into its catalog <see cref="AppError"/>.</summary>
    /// <param name="toError">Produces the <see cref="AppError"/> for the failure case.</param>
    public Result<TSuccess> ToResult(Func<TFailure, AppError> toError)
    {
        ArgumentNullException.ThrowIfNull(toError);

        return Match(Result<TSuccess>.Ok, error => Result<TSuccess>.Fail(toError(error)));
    }
}
