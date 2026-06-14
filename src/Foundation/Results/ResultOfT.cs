using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Represents the outcome of an operation that returns a <typeparamref name="T"/> value.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public abstract record Result<T>
{
    private Result() { }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>The operation completed successfully with a value.</summary>
    /// <param name="Value">The value returned by the operation.</param>
    public sealed record Success(T Value) : Result<T>;

    /// <summary>The operation failed — carries the error.</summary>
    /// <param name="Error">The error describing the failure.</param>
    public sealed record Failure(DomainError Error) : Result<T>;

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to return.</param>
    public static Success Ok(T value) => new(value);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static Failure Fail(DomainError error) => new(error);

    /// <summary>Collapses the result into a single value by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked with the value when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the error when the operation failed.</param>
    /// <returns>The value produced by whichever handler ran.</returns>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<DomainError, TOut> onFailure) => this switch
    {
        Success success => onSuccess(success.Value),
        Failure failure => onFailure(failure.Error),
        _ => throw new InvalidOperationException("Result<T> has only Success and Failure cases.")
    };

    /// <summary>Transforms the success value, propagating a failure unchanged.</summary>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="selector">Projects the success value into the new value.</param>
    /// <returns>A success carrying the mapped value, or the original failure.</returns>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector) =>
        Match<Result<TOut>>(value => Result<TOut>.Ok(selector(value)), Result<TOut>.Fail);
}
