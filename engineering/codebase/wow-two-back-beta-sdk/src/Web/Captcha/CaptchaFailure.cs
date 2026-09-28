namespace WoW.Two.Sdk.Backend.Beta.Web.Captcha;

/// <summary>Refers to why a captcha check failed.</summary>
public enum CaptchaFailure
{
    /// <summary>Refers to no failure: the token passed.</summary>
    None,

    /// <summary>Refers to a request without a token.</summary>
    Missing,

    /// <summary>Refers to a token the provider rejected: wrong, expired or already used.</summary>
    Rejected,

    /// <summary>Refers to a token solved on a hostname the site does not expect.</summary>
    Hostname,

    /// <summary>Refers to a token solved for another action.</summary>
    Action,

    /// <summary>Refers to a score under <see cref="CaptchaOptions.MinimumScore"/>.</summary>
    Score,

    /// <summary>Refers to a provider that could not be reached or answered unreadably.</summary>
    Unavailable,
}
