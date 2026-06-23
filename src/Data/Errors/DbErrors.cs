using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Data.Errors;

/// <summary>Provides the static, DI-free convenience for translating an Npgsql/EF Core exception into an <see cref="AppError"/> at a database catch site; shares its switch with <see cref="DbExceptionMappingRule"/>.</summary>
public static class DbErrors
{
    /// <summary>Translates <paramref name="exception"/> into an <see cref="AppError"/>, falling back to <see cref="AppErrorType.Unexpected"/> for a non-database exception.</summary>
    /// <param name="exception">The caught database exception.</param>
    public static AppError From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return DbExceptionMappingRule.Classify(exception) is { } type
            ? AppError.FromException(type, ErrorMessages.For(type), exception)
            : AppErrors.Unexpected(inner: exception);
    }
}
