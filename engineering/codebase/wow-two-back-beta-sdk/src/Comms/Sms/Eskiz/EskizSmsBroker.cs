using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;

/// <summary>
/// Integrates the Eskiz gateway (notify.eskiz.uz) as the SMS provider. Signs in with the account credentials, keeps the
/// bearer token for the broker's lifetime and signs in again once when a send answers <c>401</c>.
/// Eskiz sends only moderated message templates; unapproved text is rejected by the provider.
/// </summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddEskizSmsBroker</c>.</param>
/// <param name="options">Credentials, sender and callback.</param>
/// <param name="smsOptions">Cross-provider defaults.</param>
public sealed class EskizSmsBroker(IHttpClientFactory httpClientFactory, EskizSmsOptions options, SmsOptions smsOptions) : ISmsBroker, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.sms.eskiz";

    private readonly SemaphoreSlim _signIn = new(1, 1);
    private string? _token;

    /// <inheritdoc />
    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            var token = _token ?? await SignInAsync(http, null, cancellationToken);
            using var response = await PostAsync(http, token, message, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return await ReadAsync(response, cancellationToken);

            token = await SignInAsync(http, token, cancellationToken);
            using var retry = await PostAsync(http, token, message, cancellationToken);
            return await ReadAsync(retry, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new SmsSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    /// <inheritdoc />
    public void Dispose() => _signIn.Dispose();

    private async Task<HttpResponseMessage> PostAsync(HttpClient http, string token, SmsMessage message, CancellationToken cancellationToken)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("mobile_phone", SmsServiceCollectionExtensions.ToDigits(message.To)),
            new("message", message.Body),
            new("from", message.From ?? smsOptions.DefaultFrom ?? options.From),
        };
        if (options.CallbackUrl is not null)
            fields.Add(new("callback_url", options.CallbackUrl.ToString()));

        using var request = new HttpRequestMessage(HttpMethod.Post, "message/sms/send") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request, cancellationToken);
    }

    private static async Task<SmsSendResult> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            return new SmsSendResult { Success = false, FailureReason = $"eskiz_{(int)response.StatusCode}: {detail}" };
        }

        var body = await response.Content.ReadFromJsonAsync<EskizSendResponse>(cancellationToken);
        return body?.Id is { } id
            ? new SmsSendResult { Success = true, ProviderMessageId = id }
            : new SmsSendResult { Success = false, FailureReason = $"eskiz_unexpected: {body?.Message}" };
    }

    /// <summary>Signs in unless another caller already replaced <paramref name="stale"/>.</summary>
    private async Task<string> SignInAsync(HttpClient http, string? stale, CancellationToken cancellationToken)
    {
        await _signIn.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _token != stale)
                return _token;

            using var content = new MultipartFormDataContent
            {
                { new StringContent(options.Email), "email" },
                { new StringContent(options.Password), "password" },
            };
            using var response = await http.PostAsync("auth/login", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<EskizLoginResponse>(cancellationToken);
            return _token = body?.Data?.Token ?? throw new InvalidOperationException("Eskiz sign-in returned no token.");
        }
        finally
        {
            _signIn.Release();
        }
    }

    private sealed record EskizLoginResponse
    {
        [JsonPropertyName("data")]
        public EskizTokenData? Data { get; init; }
    }

    private sealed record EskizTokenData
    {
        [JsonPropertyName("token")]
        public string? Token { get; init; }
    }

    private sealed record EskizSendResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
