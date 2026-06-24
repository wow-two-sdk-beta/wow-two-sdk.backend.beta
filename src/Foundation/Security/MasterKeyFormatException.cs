namespace WoW.Two.Sdk.Backend.Beta.Foundation.Security;

/// <summary>Thrown when a configured master key is present but malformed — invalid base64 or the wrong decoded length.</summary>
/// <remarks>Deliberately carries no key material in its message, so it is safe to log. A <em>missing</em> key is not an error (the seal keeper stays sealed); only a <em>broken</em> one raises this.</remarks>
public sealed class MasterKeyFormatException : Exception
{
    /// <summary>Initializes a new instance with a descriptive, key-free message.</summary>
    /// <param name="message">A message describing the format problem without disclosing key material.</param>
    public MasterKeyFormatException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the underlying cause.</summary>
    /// <param name="message">A message describing the format problem without disclosing key material.</param>
    /// <param name="innerException">The exception that triggered this one — e.g. a decoding failure.</param>
    public MasterKeyFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
