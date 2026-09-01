using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>No-op <see cref="IWebhookDeliveryLog"/> — the default when no delivery observer is registered.</summary>
internal sealed class NoopWebhookDeliveryLog : IWebhookDeliveryLog
{
    public ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
