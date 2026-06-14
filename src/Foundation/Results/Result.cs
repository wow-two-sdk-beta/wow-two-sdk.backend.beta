using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Represents the outcome of an operation that returns no value.</summary>
public abstract record Result
{
    private Result() { }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>The operation completed successfully.</summary>
    public sealed record Success : Result;

    /// <summary>The operation failed — carries the error.</summary>
    /// <param name="Error">The error describing the failure.</param>
    public sealed record Failure(DomainError Error) : Result;

    /// <summary>Creates a successful result.</summary>
    public static Success Ok() => new();

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static Failure Fail(DomainError error) => new(error);

    /// <summary>Collapses the result into a single value by invoking the handler for whichever case occurred.</summary>
    /// <typeparam name="TOut">The type both handlers project to.</typeparam>
    /// <param name="onSuccess">Invoked when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the error when the operation failed.</param>
    /// <returns>The value produced by whichever handler ran.</returns>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<DomainError, TOut> onFailure) => this switch
    {
        Success _ => onSuccess(),
        Failure failure => onFailure(failure.Error),
        _ => throw new InvalidOperationException("Result has only Success and Failure cases.")
    };

    /// <summary>Lifts the outcome into a value-carrying result, running <paramref name="selector"/> on success and propagating a failure unchanged.</summary>
    /// <typeparam name="TOut">The value type to lift into.</typeparam>
    /// <param name="selector">Produces the success value when the operation succeeded.</param>
    /// <returns>A success carrying the produced value, or the original failure.</returns>
    public Result<TOut> Map<TOut>(Func<TOut> selector) =>
        Match<Result<TOut>>(() => Result<TOut>.Ok(selector()), Result<TOut>.Fail);
}
