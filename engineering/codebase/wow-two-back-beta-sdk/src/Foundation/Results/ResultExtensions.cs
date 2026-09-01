using System.Diagnostics.CodeAnalysis;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Results;

/// <summary>Provides bridge extensions converting a result to the throw path.</summary>
public static class ResultExtensions
{
    /// <summary>Returns the success value, or throws the failure as an <see cref="AppException"/>.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to unwrap.</param>
    public static T ValueOrThrow<T>(this Result<T> result) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            Result<T>.Success success => success.Value,
            Result<T>.Failure failure => throw failure.Error.ToException(),
            _ => throw new InvalidOperationException("Result<T> has only Success and Failure cases."),
        };
    }

    /// <summary>Throws the failure as an <see cref="AppException"/> when the operation failed; otherwise returns.</summary>
    /// <param name="result">The result to inspect.</param>
    public static void ThrowIfFailure(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result is Result.Failure failure)
        {
            throw failure.Error.ToException();
        }
    }

    /// <summary>Tests for failure, handing back the error on failure and the value on success, so a caller can guard and propagate in one line.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="error">The failure's error, set only when this returns <see langword="true"/>.</param>
    /// <param name="value">The success value, set only when this returns <see langword="false"/>.</param>
    /// <remarks>Reads as a guard clause, so a failure propagates without a cast at each hop.</remarks>
    public static bool IsFailure<T>(
        this Result<T> result,
        [NotNullWhen(true)] out AppError? error,
        [MaybeNullWhen(true)] out T value) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result is Result<T>.Failure failure)
        {
            error = failure.Error;
            value = default;
            return true;
        }

        error = null;
        value = ((Result<T>.Success)result).Value;
        return false;
    }
}
