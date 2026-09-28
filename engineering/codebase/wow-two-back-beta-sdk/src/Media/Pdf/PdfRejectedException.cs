namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents a PDF refused: unreadable, locked, over a configured limit, or asked for pages it lacks.</summary>
public sealed class PdfRejectedException : Exception
{
    /// <summary>Creates the exception with a reason code and a message.</summary>
    /// <param name="reason">A stable code, such as <c>pdf_password_required</c>.</param>
    /// <param name="message">The message.</param>
    public PdfRejectedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a reason code, a message and a cause.</summary>
    /// <param name="reason">A stable code.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public PdfRejectedException(string reason, string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public PdfRejectedException()
        : this("pdf_rejected", "The PDF was rejected.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public PdfRejectedException(string message)
        : this("pdf_rejected", message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public PdfRejectedException(string message, Exception innerException)
        : this("pdf_rejected", message, innerException)
    {
    }

    /// <summary>Gets the stable reason code: <c>pdf_unreadable</c>, <c>pdf_password_required</c>, <c>pdf_too_large</c>, …</summary>
    public string Reason { get; }
}
