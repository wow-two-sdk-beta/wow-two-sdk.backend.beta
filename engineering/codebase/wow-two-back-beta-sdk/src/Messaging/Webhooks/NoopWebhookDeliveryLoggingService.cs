using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Provides the default delivery logging, which records nothing.</summary>
internal sealed class NoopWebhookDeliveryLoggingService : IWebhookDeliveryLoggingService
{
    public ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
