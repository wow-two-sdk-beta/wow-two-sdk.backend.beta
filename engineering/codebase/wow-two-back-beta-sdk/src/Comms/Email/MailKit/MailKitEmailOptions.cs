using MailKit.Security;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.MailKit;

/// <summary>Holds sMTP connection settings for the MailKit sender.</summary>
public sealed record MailKitEmailOptions
{
    /// <summary>SMTP host. Required.</summary>
    public string Host { get; set; } = "";

    /// <summary>SMTP port. Default 587 (submission with STARTTLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>TLS negotiation mode. Default <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</summary>
    public SecureSocketOptions SecureSocket { get; set; } = SecureSocketOptions.StartTlsWhenAvailable;

    /// <summary>SMTP username. Leave empty for unauthenticated relays (dev mailpit/mailhog).</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password / app password / API key.</summary>
    public string? Password { get; set; }
}
