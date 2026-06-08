namespace WoW.Two.Sdk.Backend.Beta.Validation;

/// <summary>Represents validation failure raised as an exception.</summary>
public sealed class ValidationException : Exception
{
    /// <summary>Gets the validation errors that caused the failure.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>Initializes a new instance with no errors.</summary>
    public ValidationException() : this([]) { }

    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">The error message.</param>
    public ValidationException(string message) : base(message) => Errors = [];

    /// <summary>Initializes a new instance with the given message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ValidationException(string message, Exception innerException) : base(message, innerException) => Errors = [];

    /// <summary>Initializes a new instance carrying the validation errors that caused the failure.</summary>
    /// <param name="errors">The validation errors found.</param>
    public ValidationException(IReadOnlyList<ValidationError> errors) : base("Validation failed.") => Errors = errors;
}
