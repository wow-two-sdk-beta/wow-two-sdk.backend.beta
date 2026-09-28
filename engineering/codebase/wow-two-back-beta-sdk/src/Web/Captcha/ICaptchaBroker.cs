namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Defines the call that asks the captcha provider about a token; failures come back as results, not exceptions.</summary>
public interface ICaptchaBroker
{
    /// <summary>Verifies <paramref name="token"/> with the provider.</summary>
    /// <param name="token">The token the widget produced.</param>
    /// <param name="remoteIp">The client address, when it is shared with the provider.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<CaptchaVerifyResult> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default);
}
