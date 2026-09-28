using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Defines the captcha check of a request: find the token, ask the provider, apply the site's rules.</summary>
public interface ICaptchaValidator
{
    /// <summary>Validates the request's captcha token; passes every request while checks are off.</summary>
    /// <param name="context">The request.</param>
    /// <param name="action">The action the token must have been solved for; null accepts any.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<CaptchaResult> ValidateAsync(HttpContext context, string? action = null, CancellationToken cancellationToken = default);
}
