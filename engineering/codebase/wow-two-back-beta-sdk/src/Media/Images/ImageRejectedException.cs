namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents an input image refused before editing: unreadable, unsupported or over a configured limit.</summary>
public sealed class ImageRejectedException : Exception
{
    /// <summary>Creates the exception with a reason code and a message.</summary>
    /// <param name="reason">A stable code, such as <c>image_too_large</c>.</param>
    /// <param name="message">The message.</param>
    public ImageRejectedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public ImageRejectedException()
        : this("image_rejected", "The image was rejected.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public ImageRejectedException(string message)
        : this("image_rejected", message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ImageRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = "image_rejected";
    }

    /// <summary>Gets the stable reason code: <c>image_unreadable</c>, <c>image_too_large</c>, <c>image_format_unwritable</c>, …</summary>
    public string Reason { get; }
}
