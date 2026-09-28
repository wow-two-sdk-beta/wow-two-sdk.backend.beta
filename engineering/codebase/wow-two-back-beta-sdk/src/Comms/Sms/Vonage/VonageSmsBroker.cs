using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;

/// <summary>Integrates the Vonage SMS API as the SMS provider; a message counts as sent when every part reports status <c>0</c>.</summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddVonageSmsBroker</c>.</param>
/// <param name="options">Credentials.</param>
/// <param name="smsOptions">Cross-provider defaults.</param>
public sealed class VonageSmsBroker(IHttpClientFactory httpClientFactory, VonageSmsOptions options, SmsOptions smsOptions) : ISmsBroker
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.sms.vonage";

    /// <inheritdoc />
    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var from = message.From ?? smsOptions.DefaultFrom;
        if (from is null)
            return new SmsSendResult { Success = false, FailureReason = "no_from_address" };

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(
            [
                new("api_key", options.ApiKey),
                new("api_secret", options.ApiSecret),
                new("from", from),
                new("to", SmsServiceCollectionExtensions.ToDigits(message.To)),
                new("text", message.Body),
                new("type", "unicode"),
            ]);
            using var response = await http.PostAsync("sms/json", content, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<VonageResponse>(cancellationToken);
            var parts = body?.Messages ?? [];
            if (response.IsSuccessStatusCode && parts.Count > 0 && parts.All(part => part.Status == "0"))
                return new SmsSendResult { Success = true, ProviderMessageId = parts[0].MessageId };

            var failed = parts.FirstOrDefault(part => part.Status != "0");
            return new SmsSendResult { Success = false, FailureReason = $"vonage_{failed?.Status ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture)}: {failed?.ErrorText}" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new SmsSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    private sealed record VonageResponse
    {
        [JsonPropertyName("messages")]
        public List<VonagePart>? Messages { get; init; }
    }

    private sealed record VonagePart
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("message-id")]
        public string? MessageId { get; init; }

        [JsonPropertyName("error-text")]
        public string? ErrorText { get; init; }
    }
}
