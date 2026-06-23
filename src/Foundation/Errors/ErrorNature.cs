namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the descriptive nature of a failure — consumers derive retry, fallback, and log decisions from it.</summary>
public enum ErrorNature
{
    /// <summary>Temporary; retrying may succeed (timeout, 503, 429).</summary>
    Transient,

    /// <summary>Stable; retrying the same call will not help (validation, 404, 401).</summary>
    Permanent,

    /// <summary>An internal bug — our fault, never retried (unexpected, deserialization).</summary>
    Defect,
}

/// <summary>Defines the contract for classifying an <see cref="AppErrorType"/> into its <see cref="ErrorNature"/>.</summary>
public interface IErrorNatureClassifier
{
    /// <summary>Classifies <paramref name="type"/> into its descriptive nature.</summary>
    /// <param name="type">The failure kind.</param>
    ErrorNature Classify(AppErrorType type);
}

/// <summary>Provides the default classification; apps override via DI to tune retry/fallback.</summary>
public sealed class DefaultErrorNatureClassifier : IErrorNatureClassifier
{
    /// <inheritdoc/>
    public ErrorNature Classify(AppErrorType type)
    {
        return type switch
        {
            AppErrorType.DbTimeout or AppErrorType.OperationTimeout
                or AppErrorType.ExternalUnavailable or AppErrorType.TooManyRequests => ErrorNature.Transient,
            AppErrorType.Unexpected or AppErrorType.SerializationFailed
                or AppErrorType.FileNotFound or AppErrorType.DataIntegrity => ErrorNature.Defect,
            _ => ErrorNature.Permanent,
        };
    }
}
