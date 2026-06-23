using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Provides extensions that invoke a delegate and capture its outcome as a <see cref="Result{T}"/>.</summary>
public static class AttemptExtensions
{
    /// <summary>Invokes <paramref name="operation"/> and captures success or failure into a result.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="operation">The operation to invoke.</param>
    public static Result<T> Attempt<T>(this Func<T> operation) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            return Result<T>.Ok(operation());
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Fail(AppErrors.Canceled());
        }
        catch (AppException appException)
        {
            return Result<T>.Fail(appException.Error);
        }
        catch (Exception exception)
        {
            return Result<T>.Fail(AppErrors.Unexpected(inner: exception));
        }
    }

    /// <summary>Invokes the asynchronous <paramref name="operation"/> and captures its outcome.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="operation">The asynchronous operation to invoke.</param>
    public static async ValueTask<Result<T>> AttemptAsync<T>(this Func<Task<T>> operation) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var value = await operation().ConfigureAwait(false);

            return Result<T>.Ok(value);
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Fail(AppErrors.Canceled());
        }
        catch (AppException appException)
        {
            return Result<T>.Fail(appException.Error);
        }
        catch (Exception exception)
        {
            return Result<T>.Fail(AppErrors.Unexpected(inner: exception));
        }
    }
}
