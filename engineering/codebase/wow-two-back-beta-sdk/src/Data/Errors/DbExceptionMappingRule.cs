using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Data.Errors;

/// <summary>Maps Npgsql and EF Core exceptions to an <see cref="AppError"/> — the SDK's built-in <see cref="IExceptionMappingRule"/> for the data layer.</summary>
public sealed class DbExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc/>
    public AppError? TryMap(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Classify(exception) is { } type
            ? AppError.FromException(type, ErrorMessageConstants.For(type), exception)
            : null;
    }

    /// <summary>Classifies a database exception into its <see cref="AppErrorType"/>, or <see langword="null"/> when it is not a recognized Npgsql/EF failure.</summary>
    /// <param name="exception">The caught exception.</param>
    internal static AppErrorType? Classify(Exception exception)
    {
        return exception switch
        {
            TimeoutException => AppErrorType.DbTimeout,
            DbUpdateConcurrencyException => AppErrorType.Conflict,
            DbUpdateException { InnerException: { } cause } => Classify(cause),
            PostgresException { SqlState: "23505" } => AppErrorType.Conflict,
            PostgresException { SqlState: "57P03" or "53300" } => AppErrorType.ExternalUnavailable,
            PostgresException => null,
            NpgsqlException { InnerException: TimeoutException } => AppErrorType.DbTimeout,
            NpgsqlException { InnerException: OperationCanceledException } => null,
            NpgsqlException => AppErrorType.ExternalUnavailable,
            _ => null,
        };
    }
}
