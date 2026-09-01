using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Defines the webhook signature scheme, so a subscriber's required scheme is a registration choice.</summary>
public interface IWebhookSignatureHasher
{
    /// <summary>Gets the scheme prefix this hasher stamps on every signature.</summary>
    string Scheme { get; }

    /// <summary>Compute the <see cref="WebhookHeaderConstants.Signature"/> value for a payload.</summary>
    /// <param name="secret">The subscription's secret.</param>
    /// <param name="timestamp">The unix-seconds timestamp (also sent as <see cref="WebhookHeaderConstants.Timestamp"/>).</param>
    /// <param name="payload">The request body bytes — signed verbatim.</param>
    string Create(string secret, string timestamp, ReadOnlySpan<byte> payload);
}
