namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Holds the captcha providers the siteverify broker speaks to.</summary>
public static class CaptchaProviderNameConstants
{
    /// <summary>Holds <c>turnstile</c> — Cloudflare Turnstile.</summary>
    public const string Turnstile = "turnstile";

    /// <summary>Holds <c>hcaptcha</c> — hCaptcha.</summary>
    public const string HCaptcha = "hcaptcha";

    /// <summary>Holds <c>recaptcha</c> — Google reCAPTCHA v2 and v3.</summary>
    public const string ReCaptcha = "recaptcha";
}
