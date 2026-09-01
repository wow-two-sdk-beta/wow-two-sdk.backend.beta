
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides the default exception mapping: unwraps a carried <see cref="AppException"/>, then applies the registered <see cref="IExceptionMappingRule"/> contributors (last-registered first, so an app rule shadows an SDK rule), then falls back to <see cref="AppErrorType.Unexpected"/>.</summary>
public sealed class ExceptionMapper : IExceptionMapper
{
    private readonly IExceptionMappingRule[] _rules;

    /// <summary>Initializes the mapper from the registered rules; later registrations take precedence.</summary>
    /// <param name="rules">The registered mapping rules, in registration order.</param>
    public ExceptionMapper(IEnumerable<IExceptionMappingRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = rules as IExceptionMappingRule[] ?? [.. rules];
    }

    /// <inheritdoc/>
    public AppError Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is AppException appException)
        {
            return appException.Error;
        }

        // Last-registered rule wins, so an app-registered rule shadows an SDK rule for the same exception.
        for (var index = _rules.Length - 1; index >= 0; index--)
        {
            var error = _rules[index].TryMap(exception);
            if (error is not null)
            {
                return error;
            }
        }

        return AppErrorFactory.Unexpected(inner: exception);
    }
}
