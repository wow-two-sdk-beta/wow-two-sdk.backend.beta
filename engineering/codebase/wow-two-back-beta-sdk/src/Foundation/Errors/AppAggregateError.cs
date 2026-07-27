namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents a failure aggregating several underlying <see cref="AppError"/> instances.</summary>
public sealed record AppAggregateError : AppError
{
    /// <summary>Gets the underlying errors aggregated by this failure.</summary>
    public required IReadOnlyList<AppError> Errors { get; init; }

    /// <summary>Creates an aggregate error from <paramref name="errors"/>, defaulting <see cref="AppError.Type"/> to <see cref="AppErrorType.Unexpected"/>.</summary>
    /// <param name="errors">The underlying errors to aggregate; must not be empty.</param>
    public static AppAggregateError From(IReadOnlyList<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("An aggregate error requires at least one underlying error.", nameof(errors));
        }

        return new AppAggregateError
        {
            Type = AppErrorType.Unexpected,
            Message = "Multiple errors occurred.",
            Errors = errors,
        };
    }
}
