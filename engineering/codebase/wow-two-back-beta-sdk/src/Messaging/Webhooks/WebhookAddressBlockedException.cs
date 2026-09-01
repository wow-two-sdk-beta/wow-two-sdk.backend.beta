using System.Net;
using System.Net.Sockets;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Thrown by the guarded connect callback when a webhook target resolves only to blocked (private / loopback / link-local) addresses.</summary>
internal sealed class WebhookAddressBlockedException(string host)
    : Exception($"Webhook target host '{host}' resolves only to blocked (private/loopback/link-local) addresses.")
{
    /// <summary>The rejected host.</summary>
    public string Host { get; } = host;
}
