using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>
/// Validates a request's captcha token: from the configured header, else a known form field; then the provider's
/// verdict, hostname, action and score. The token is never read from a JSON body, which the handler binds.
/// </summary>
/// <param name="broker">Asks the provider.</param>
/// <param name="options">Rules; <c>Web:Captcha</c> reloads live.</param>
/// <param name="logger">Records failures and outages.</param>
public sealed partial class CaptchaValidator(ICaptchaBroker broker, IOptionsMonitor<CaptchaOptions> options, ILogger<CaptchaValidator> logger) : ICaptchaValidator
{
    /// <inheritdoc />
    public async Task<CaptchaResult> ValidateAsync(HttpContext context, string? action = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = options.CurrentValue;
        if (!current.Enabled)
            return new CaptchaResult { Failure = CaptchaFailure.None };

        var token = await TokenAsync(context.Request, current, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return Fail(CaptchaFailure.Missing, null);

        var verification = await broker.VerifyAsync(token, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (verification.Unavailable)
        {
            LogUnavailable(logger, current.Provider, current.FailOpen);
            return current.FailOpen ? new CaptchaResult { Failure = CaptchaFailure.None, Verification = verification } : Fail(CaptchaFailure.Unavailable, verification);
        }

        if (!verification.Success)
            return Fail(CaptchaFailure.Rejected, verification);
        if (current.ExpectedHostnames.Count > 0 && !current.ExpectedHostnames.Contains(verification.Hostname ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            return Fail(CaptchaFailure.Hostname, verification);
        if (action is not null && !string.Equals(action, verification.Action, StringComparison.Ordinal))
            return Fail(CaptchaFailure.Action, verification);
        if (verification.Score is { } score && score < current.MinimumScore)
            return Fail(CaptchaFailure.Score, verification);

        return new CaptchaResult { Failure = CaptchaFailure.None, Verification = verification };
    }

    private CaptchaResult Fail(CaptchaFailure failure, CaptchaVerifyResult? verification)
    {
        LogFailed(logger, failure, string.Join(",", verification?.ErrorCodes ?? []));
        return new CaptchaResult { Failure = failure, Verification = verification };
    }

    private static async Task<string?> TokenAsync(HttpRequest request, CaptchaOptions options, CancellationToken cancellationToken)
    {
        if (request.Headers.TryGetValue(options.TokenHeader, out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString();
        if (!request.HasFormContentType)
            return null;

        var form = await request.ReadFormAsync(cancellationToken);
        return options.TokenFormFields.Select(field => form[field].ToString()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Captcha check failed: {Failure} ({ErrorCodes}).")]
    private static partial void LogFailed(ILogger logger, CaptchaFailure failure, string errorCodes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Captcha provider {Provider} is unavailable; failing {Mode}.")]
    private static partial void LogUnavailable(ILogger logger, string provider, bool mode);
}
