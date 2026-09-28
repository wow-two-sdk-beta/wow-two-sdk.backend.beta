using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;

/// <summary>Integrates the Twilio Messages API as the SMS provider.</summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddTwilioSmsBroker</c>.</param>
/// <param name="options">Credentials and sender.</param>
/// <param name="smsOptions">Cross-provider defaults.</param>
public sealed class TwilioSmsBroker(IHttpClientFactory httpClientFactory, TwilioSmsOptions options, SmsOptions smsOptions) : ISmsBroker
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.sms.twilio";

    /// <inheritdoc />
    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var fields = new List<KeyValuePair<string, string>> { new("To", message.To.Trim()), new("Body", message.Body) };
        var from = message.From ?? smsOptions.DefaultFrom;
        if (!string.IsNullOrWhiteSpace(options.MessagingServiceSid) && message.From is null)
            fields.Add(new("MessagingServiceSid", options.MessagingServiceSid));
        else if (from is not null)
            fields.Add(new("From", from));
        else
            return new SmsSendResult { Success = false, FailureReason = "no_from_address" };

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"2010-04-01/Accounts/{Uri.EscapeDataString(options.AccountSid)}/Messages.json")
            {
                Content = new FormUrlEncodedContent(fields),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}")));

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<TwilioMessageResponse>(cancellationToken);
            return response.IsSuccessStatusCode && body?.Sid is { } sid
                ? new SmsSendResult { Success = true, ProviderMessageId = sid }
                : new SmsSendResult { Success = false, FailureReason = $"twilio_{body?.Code ?? (int)response.StatusCode}: {body?.Message}" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new SmsSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    private sealed record TwilioMessageResponse
    {
        [JsonPropertyName("sid")]
        public string? Sid { get; init; }

        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
