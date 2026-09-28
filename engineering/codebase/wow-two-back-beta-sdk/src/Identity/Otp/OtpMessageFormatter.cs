using System.Globalization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Localization;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// Formats one-time codes into message text and a subject line from <see cref="OtpMessageOptions"/> templates, falling
/// back through the culture's parents, the default culture and the built-in <c>en</c>/<c>ru</c>/<c>uz</c> wording.
/// </summary>
/// <param name="options">The OTP options; the <c>Identity:Otp:Messages</c> section reloads live.</param>
public sealed class OtpMessageFormatter(IOptionsMonitor<OtpOptions> options) : IOtpMessageFormatter
{
    /// <inheritdoc />
    public OtpMessageModel Format(string purpose, string channel, string code, TimeSpan lifetime, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var messages = options.CurrentValue.Messages;
        culture ??= CultureInfo.CurrentUICulture;
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["minutes"] = (int)Math.Ceiling(lifetime.TotalMinutes),
            ["app"] = messages.AppName,
            ["purpose"] = purpose,
        };

        return new OtpMessageModel
        {
            Text = Render(messages, culture, [$"{purpose}.{channel}", purpose, channel, "text"], arguments),
            Subject = Render(messages, culture, [$"{purpose}.{channel}.subject", $"{purpose}.subject", $"{channel}.subject", "subject"], arguments),
        };
    }

    private static string Render(OtpMessageOptions messages, CultureInfo culture, string[] keys, Dictionary<string, object?> arguments)
    {
        foreach (var candidate in Cultures(culture, messages.DefaultCulture))
        {
            foreach (var template in Templates(messages, candidate.Name, keys))
            {
                if (MessageTemplateMapper.TryFormat(template, arguments, candidate, out var message))
                    return message;
            }
        }

        throw new InvalidOperationException($"No one-time-code template renders the keys '{string.Join("', '", keys)}'.");
    }

    /// <summary>The configured templates for <paramref name="culture"/>, then the built-in ones, most specific key first.</summary>
    private static IEnumerable<string> Templates(OtpMessageOptions messages, string culture, string[] keys)
    {
        if (messages.Templates.TryGetValue(culture, out var configured))
        {
            foreach (var key in keys)
            {
                if (configured.TryGetValue(key, out var template) && !string.IsNullOrWhiteSpace(template))
                    yield return template;
            }
        }

        if (OtpMessageTemplateConstants.Templates.TryGetValue(culture, out var builtIn))
        {
            foreach (var key in keys)
            {
                if (builtIn.TryGetValue(key, out var template))
                    yield return template;
            }
        }
    }

    /// <summary>The requested culture and its parents, then the default culture and its parents, then <c>en</c>.</summary>
    private static IEnumerable<CultureInfo> Cultures(CultureInfo requested, string defaultCulture)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { requested, CultureOf(defaultCulture), CultureInfo.GetCultureInfo("en") })
        {
            for (var culture = start; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
            {
                if (seen.Add(culture.Name))
                    yield return culture;
            }
        }
    }

    private static CultureInfo CultureOf(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
