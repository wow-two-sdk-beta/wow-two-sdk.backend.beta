using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Represents the thrown form of a <see cref="ValidationError"/>.</summary>
public sealed class ValidationException : AppException
{
    /// <summary>Initializes a new instance carrying <paramref name="error"/>.</summary>
    /// <param name="error">The aggregate validation error.</param>
    public ValidationException(ValidationError error)
        : base(error)
    {
    }

    /// <summary>Gets the aggregate validation error this exception carries.</summary>
    public ValidationError ValidationError => (ValidationError)Error;
}
