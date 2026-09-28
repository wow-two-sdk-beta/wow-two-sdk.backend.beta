using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Images;

namespace WoW.Two.Sdk.Backend.Beta.Media.Errors;

/// <summary>Maps media refusals — an unreadable or oversized image, an invalid or locked PDF — to validation errors.</summary>
public sealed class MediaExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            ImageRejectedException rejected => AppError.Of(AppErrorType.Validation, rejected.Message, MessageKey(rejected.Reason)),
            _ => null,
        };
    }

    private static Dictionary<string, object?> MessageKey(string reason)
        => new(StringComparer.Ordinal) { ["messageKey"] = reason };
}
