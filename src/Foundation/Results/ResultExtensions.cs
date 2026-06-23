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
}
