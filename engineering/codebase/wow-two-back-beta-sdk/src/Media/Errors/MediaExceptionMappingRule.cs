using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Images;
using WoW.Two.Sdk.Backend.Beta.Media.Pdf;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Word;

namespace WoW.Two.Sdk.Backend.Beta.Media.Errors;

/// <summary>Maps media refusals — an unreadable or oversized image, PDF or Word file, a bad import row — to validation errors.</summary>
public sealed class MediaExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            ImageRejectedException rejected => AppError.Of(AppErrorType.Validation, rejected.Message, MessageKey(rejected.Reason)),
            PdfRejectedException rejected => AppError.Of(AppErrorType.Validation, rejected.Message, MessageKey(rejected.Reason)),
            WordRejectedException rejected => AppError.Of(AppErrorType.Validation, rejected.Message, MessageKey(rejected.Reason)),
            TabularRowException row => AppError.Of(AppErrorType.Validation, row.Message, MessageKey("tabular_row_invalid")),
            _ => null,
        };
    }

    private static Dictionary<string, object?> MessageKey(string reason)
        => new(StringComparer.Ordinal) { ["messageKey"] = reason };
}
