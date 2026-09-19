namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors.Mappers;

/// <summary>Maps an AppErrorType to its descriptive ErrorNature; apps override through DI.</summary>
public sealed class ErrorNatureMapper : IErrorNatureMapper
{
    /// <inheritdoc/>
    public ErrorNature Map(AppErrorType type)
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
