using System.Security.Cryptography;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Holds well-known defaults for webhook delivery.</summary>
public static class WebhookDefaultConstants
{
    /// <summary>Holds name of the delivery <c>HttpClient</c> resolved from <c>IHttpClientFactory</c>. Add handlers/policies via <c>AddHttpClient(WebhookDefaultConstants.HttpClientName)</c>.</summary>
    public const string HttpClientName = "webhooks";
}
