using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>
/// Integrates the siteverify protocol Turnstile, hCaptcha and reCAPTCHA share: a form POST of secret, token and client
/// address, answered with JSON. An unreachable or unreadable provider comes back as <see cref="CaptchaVerifyResult.Unavailable"/>.
/// </summary>
/// <param name="httpClientFactory">Creates the named client registered by <c>AddCaptcha</c>.</param>
/// <param name="options">The provider and secret; <c>Web:Captcha</c> reloads live.</param>
public sealed class SiteVerifyCaptchaBroker(IHttpClientFactory httpClientFactory, IOptionsMonitor<CaptchaOptions> options) : ICaptchaBroker
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.captcha";

    private static readonly Dictionary<string, Uri> Endpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        [CaptchaProviderNameConstants.Turnstile] = new("https://challenges.cloudflare.com/turnstile/v0/siteverify"),
        [CaptchaProviderNameConstants.HCaptcha] = new("https://api.hcaptcha.com/siteverify"),
        [CaptchaProviderNameConstants.ReCaptcha] = new("https://www.google.com/recaptcha/api/siteverify"),
    };

    /// <inheritdoc />
    public async Task<CaptchaVerifyResult> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var current = options.CurrentValue;
        var endpoint = current.VerifyUrl ?? (Endpoints.TryGetValue(current.Provider, out var known)
            ? known
            : throw new InvalidOperationException($"Unknown captcha provider '{current.Provider}'; set Web:Captcha:VerifyUrl."));
        var fields = new List<KeyValuePair<string, string>> { new("secret", current.Secret), new("response", token) };
        if (current.SendRemoteIp && !string.IsNullOrWhiteSpace(remoteIp))
            fields.Add(new("remoteip", remoteIp));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(current.Timeout);
        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).PostAsync(endpoint, new FormUrlEncodedContent(fields), timeout.Token);
            if (!response.IsSuccessStatusCode)
                return new CaptchaVerifyResult { Success = false, Unavailable = true, ErrorCodes = [$"http-{(int)response.StatusCode}"] };

            var body = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(timeout.Token)
                ?? throw new JsonException("The provider answered with an empty body.");
            return new CaptchaVerifyResult
            {
                Success = body.Success,
                ErrorCodes = body.ErrorCodes ?? [],
                Hostname = body.Hostname,
                Action = body.Action,
                Score = body.Score,
                ChallengedAt = DateTimeOffset.TryParse(body.ChallengeTimestamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at) ? at : null,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new CaptchaVerifyResult { Success = false, Unavailable = true, ErrorCodes = ["provider-unavailable"] };
        }
    }

    private sealed record SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("score")]
        public double? Score { get; init; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; init; }
    }
}
