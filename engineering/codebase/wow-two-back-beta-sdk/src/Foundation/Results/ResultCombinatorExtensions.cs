using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>
/// Chains results without unwrapping them: <c>Bind</c> continues with another result-returning step, <c>Tap</c> runs a
/// side effect on success, <c>Ensure</c> turns a failed check into a failure, <c>ValueOr</c> supplies a fallback and
/// <c>Combine</c> gathers several outcomes. Every combinator passes an existing failure through unchanged.
/// </summary>
public static class ResultCombinatorExtensions
{
    /// <summary>Continues with <paramref name="next"/> on success.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TOut">The next step's value type.</typeparam>
    /// <param name="result">The result so far.</param>
    /// <param name="next">The next step.</param>
    public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> next)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);
        return result.Match(next, Result<TOut>.Fail);
    }

    /// <summary>Continues with an asynchronous step on success.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TOut">The next step's value type.</typeparam>
    /// <param name="result">The result so far.</param>
    /// <param name="next">The next step.</param>
    public static Task<Result<TOut>> BindAsync<T, TOut>(this Result<T> result, Func<T, Task<Result<TOut>>> next)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);
        return result.Match(next, error => Task.FromResult(Result<TOut>.Fail(error)));
    }

    /// <summary>Continues a pending result with an asynchronous step on success.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TOut">The next step's value type.</typeparam>
    /// <param name="pending">The pending result so far.</param>
    /// <param name="next">The next step.</param>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> pending, Func<T, Task<Result<TOut>>> next)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(pending);
        return await (await pending.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);
    }

    /// <summary>Continues a pending result with a synchronous step on success.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TOut">The next step's value type.</typeparam>
    /// <param name="pending">The pending result so far.</param>
    /// <param name="next">The next step.</param>
    public static async Task<Result<TOut>> Bind<T, TOut>(this Task<Result<T>> pending, Func<T, Result<TOut>> next)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(pending);
        return (await pending.ConfigureAwait(false)).Bind(next);
    }

    /// <summary>Transforms a pending result's value on success.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="pending">The pending result.</param>
    /// <param name="selector">Projects the value.</param>
    public static async Task<Result<TOut>> Map<T, TOut>(this Task<Result<T>> pending, Func<T, TOut> selector)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(pending);
        return (await pending.ConfigureAwait(false)).Map(selector);
    }

    /// <summary>Runs <paramref name="action"/> with the value on success and returns the result unchanged.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The side effect.</param>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);
        if (result is Result<T>.Success success)
            action(success.Value);

        return result;
    }

    /// <summary>Runs an asynchronous side effect with the value on success and returns the result unchanged.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The side effect.</param>
    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);
        if (result is Result<T>.Success success)
            await action(success.Value).ConfigureAwait(false);

        return result;
    }

    /// <summary>Runs <paramref name="action"/> with the error on failure and returns the result unchanged.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The side effect, typically logging.</param>
    public static Result<T> TapError<T>(this Result<T> result, Action<AppError> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);
        if (result is Result<T>.Failure failure)
            action(failure.Error);

        return result;
    }

    /// <summary>Fails with <paramref name="error"/> when a successful value does not satisfy <paramref name="predicate"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="predicate">The condition the value must meet.</param>
    /// <param name="error">The failure when it does not.</param>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, AppError error)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(error);
        return result.Ensure(predicate, _ => error);
    }

    /// <summary>Fails with the error built from the value when it does not satisfy <paramref name="predicate"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="predicate">The condition the value must meet.</param>
    /// <param name="error">Builds the failure from the rejected value.</param>
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Func<T, AppError> error)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        return result is Result<T>.Success success && !predicate(success.Value)
            ? Result<T>.Fail(error(success.Value))
            : result;
    }

    /// <summary>The value on success, otherwise <paramref name="fallback"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="fallback">The value to use on failure.</param>
    public static T ValueOr<T>(this Result<T> result, T fallback)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        return result is Result<T>.Success success ? success.Value : fallback;
    }

    /// <summary>The value on success, otherwise the fallback computed from the error.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="fallback">Computes the value from the error.</param>
    public static T ValueOr<T>(this Result<T> result, Func<AppError, T> fallback)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return result.Match(value => value, fallback);
    }

    /// <summary>Drops the value, keeping only success or the failure.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result.</param>
    public static Result ToResult<T>(this Result<T> result)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Match(_ => Result.Ok(), Result.Fail);
    }

    /// <summary>Continues a value-less result with a step that produces a value.</summary>
    /// <typeparam name="TOut">The next step's value type.</typeparam>
    /// <param name="result">The result so far.</param>
    /// <param name="next">The next step.</param>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> next)
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);
        return result.Match(next, Result<TOut>.Fail);
    }

    /// <summary>
    /// Succeeds when every result succeeded; otherwise fails with the only failure, or with an <see cref="AppAggregateError"/>
    /// holding every failure in order.
    /// </summary>
    /// <param name="results">The outcomes to combine.</param>
    public static Result Combine(this IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Failed([.. results.OfType<Result.Failure>().Select(failure => failure.Error)]) is { } error
            ? Result.Fail(error)
            : Result.Ok();
    }

    /// <summary>Collects every value when all results succeeded; otherwise fails like <see cref="Combine(IEnumerable{Result})"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="results">The outcomes to combine, in order.</param>
    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(results);
        var all = results.ToList();
        return Failed([.. all.OfType<Result<T>.Failure>().Select(failure => failure.Error)]) is { } error
            ? Result<IReadOnlyList<T>>.Fail(error)
            : Result<IReadOnlyList<T>>.Ok(all.Cast<Result<T>.Success>().Select(success => success.Value).ToList());
    }

    private static AppError? Failed(List<AppError> errors) => errors.Count switch
    {
        0 => null,
        1 => errors[0],
        _ => AppAggregateError.From(errors),
    };
}
