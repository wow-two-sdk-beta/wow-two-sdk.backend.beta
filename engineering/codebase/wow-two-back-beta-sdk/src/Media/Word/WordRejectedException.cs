namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents a Word document refused: unreadable, over a configured limit, or a template missing values.</summary>
public sealed class WordRejectedException : Exception
{
    /// <summary>Creates the exception with a reason code and a message.</summary>
    /// <param name="reason">A stable code, such as <c>word_unreadable</c>.</param>
    /// <param name="message">The message.</param>
    public WordRejectedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a reason code, a message and a cause.</summary>
    /// <param name="reason">A stable code.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public WordRejectedException(string reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public WordRejectedException()
        : this("word_rejected", "The Word document was rejected.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public WordRejectedException(string message)
        : this("word_rejected", message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public WordRejectedException(string message, Exception innerException)
        : this("word_rejected", message, innerException)
    {
    }

    /// <summary>Gets the stable reason code: <c>word_unreadable</c>, <c>word_too_large</c>, <c>word_template_value_missing</c>.</summary>
    public string Reason { get; }
}
