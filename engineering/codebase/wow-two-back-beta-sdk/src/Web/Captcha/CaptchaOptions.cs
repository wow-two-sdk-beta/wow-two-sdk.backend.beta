namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Holds the captcha provider, its secret and the checks a passing token must meet.</summary>
/// <remarks>
/// Off until <see cref="Enabled"/>: every <c>RequireCaptcha()</c> endpoint passes, so local runs need no keys. Set in
/// code with <c>AddCaptcha(o => …)</c> or in the host section <c>Web:Captcha</c>, which is applied last.
/// </remarks>
public sealed record CaptchaOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Web:Captcha";

    /// <summary>Gets or sets whether tokens are checked. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the provider: <c>turnstile</c> (default), <c>hcaptcha</c> or <c>recaptcha</c>.</summary>
    public string Provider { get; set; } = CaptchaProviderNameConstants.Turnstile;

    /// <summary>Gets or sets the secret key the provider issued for this site.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Gets or sets another verification endpoint, such as an enterprise one; null uses the provider's public URL.</summary>
    public Uri? VerifyUrl { get; set; }

    /// <summary>Gets or sets the lowest score accepted when the provider scores (reCAPTCHA v3). Default 0.5.</summary>
    public double MinimumScore { get; set; } = 0.5;

    /// <summary>Gets the hostnames a token must have been solved on; empty accepts any.</summary>
    public List<string> ExpectedHostnames { get; } = [];

    /// <summary>Gets or sets the header that carries the token. Default <c>X-Captcha-Token</c>.</summary>
    public string TokenHeader { get; set; } = "X-Captcha-Token";

    /// <summary>Gets the form fields that may carry the token, the widgets' own names first.</summary>
    public List<string> TokenFormFields { get; } = ["cf-turnstile-response", "h-captcha-response", "g-recaptcha-response", "captcha"];

    /// <summary>Gets or sets whether the client address is sent to the provider. Default true.</summary>
    public bool SendRemoteIp { get; set; } = true;

    /// <summary>Gets or sets how long the provider may take. Default 10 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets whether requests pass while the provider cannot be reached; off rejects them. Default false.</summary>
    public bool FailOpen { get; set; }
}
