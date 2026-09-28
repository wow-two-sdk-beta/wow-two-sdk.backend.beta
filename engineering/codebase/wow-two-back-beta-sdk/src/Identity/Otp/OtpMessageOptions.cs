namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Holds the templates <see cref="OtpMessageFormatter"/> words delivered codes with.</summary>
/// <remarks>
/// Templates are keyed by culture, then by the most specific key that exists: <c>{purpose}.{channel}</c>, <c>{purpose}</c>,
/// <c>{channel}</c>, <c>text</c>; subjects add <c>.subject</c> (<c>two-factor.email.subject</c> … <c>subject</c>).
/// Placeholders: <c>{code}</c>, <c>{minutes}</c>, <c>{app}</c>, <c>{purpose}</c>, plus ICU plurals such as
/// <c>{minutes, plural, one {# minute} other {# minutes}}</c>. Built-in texts cover <c>en</c>, <c>ru</c> and <c>uz</c>.
/// </remarks>
public sealed record OtpMessageOptions
{
    /// <summary>Gets or sets the application name <c>{app}</c> renders. Default <c>App</c>.</summary>
    public string AppName { get; set; } = "App";

    /// <summary>Gets or sets the culture used when the requested one has no template. Default <c>en</c>.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Gets the templates: culture → key → template.</summary>
    public Dictionary<string, Dictionary<string, string>> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);
}
