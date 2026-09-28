namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Represents a template that cannot render: missing, or missing values its placeholders name.</summary>
public sealed class EmailTemplateRejectedException : Exception
{
    /// <summary>Creates the exception with a reason code and a message.</summary>
    /// <param name="reason">A stable code, such as <c>email_template_missing</c>.</param>
    /// <param name="message">The message.</param>
    public EmailTemplateRejectedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public EmailTemplateRejectedException()
        : this("email_template_rejected", "The email template was rejected.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public EmailTemplateRejectedException(string message)
        : this("email_template_rejected", message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public EmailTemplateRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
        Reason = "email_template_rejected";
    }

    /// <summary>Gets the stable reason code: <c>email_template_missing</c> or <c>email_template_value_missing</c>.</summary>
    public string Reason { get; } = "email_template_rejected";
}
