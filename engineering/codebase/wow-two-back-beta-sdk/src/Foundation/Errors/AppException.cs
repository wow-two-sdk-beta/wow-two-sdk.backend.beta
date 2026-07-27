namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents the thrown form of an <see cref="AppError"/> — the single exception the global handler maps; subclass only to carry members a catch site reads.</summary>
public class AppException : Exception
{
    /// <summary>Initializes a new instance carrying <paramref name="error"/>.</summary>
    /// <param name="error">The error being thrown.</param>
    /// <param name="inner">Optional underlying cause.</param>
    public AppException(AppError error, Exception? inner = null)
        : base(error?.Message, inner)
    {
        ArgumentNullException.ThrowIfNull(error);

        Error = error;
    }

    /// <summary>Gets the error this exception carries.</summary>
    public AppError Error { get; }
}
