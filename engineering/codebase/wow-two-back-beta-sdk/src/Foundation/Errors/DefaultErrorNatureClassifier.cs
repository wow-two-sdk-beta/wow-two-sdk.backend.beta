namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

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
