using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>
/// Provides error-message translation per <see cref="ErrorTranslationSettings"/>, re-read on every call so a
/// configuration reload switches it live. Culture comes from the request-localization feature when that middleware ran,
/// otherwise from <c>Accept-Language</c>. Field errors try the host catalog, the built-in identity texts, then
/// FluentValidation's language pack;
/// top-level errors try their <c>messageKey</c>, then their error type in the catalog and the built-in texts.
/// A template whose placeholder has no argument yields the authored message instead.
/// </summary>
/// <param name="settings">The live <c>ErrorTranslation</c> settings.</param>
public sealed class ErrorTranslationService(IOptionsMonitor<ErrorTranslationSettings> settings) : IErrorTranslationService
{
    /// <summary>The <see cref="AppError.Metadata"/> entry naming a catalog key for a top-level message.</summary>
    public const string MessageKeyMetadata = "messageKey";

    private static readonly object CultureItemKey = new();
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private readonly ConcurrentDictionary<string, CultureInfo[]> _validatorCultures = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public CultureInfo? ResolveCulture(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = settings.CurrentValue;
        if (!current.Enabled)
            return null;

        if (context.Items.TryGetValue(CultureItemKey, out var cached))
            return cached as CultureInfo;

        var culture = Resolve(context, current);
        context.Items[CultureItemKey] = culture;
        return culture;
    }

    /// <inheritdoc />
    public bool TryTranslate(HttpContext context, string key, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? message)
    {
        message = null;
        var culture = ResolveCulture(context);
        return culture is not null
            && !string.IsNullOrEmpty(key)
            && TryCatalog(settings.CurrentValue, culture, key, out var template)
            && MessageTemplateMapper.TryFormat(template, arguments, culture, out message);
    }

    /// <inheritdoc />
    public string Translate(HttpContext context, AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Pseudo(TranslateCore(context, error));
    }

    /// <inheritdoc />
    public string Translate(HttpContext context, FieldError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Pseudo(TranslateCore(context, error));
    }

    private string TranslateCore(HttpContext context, AppError error)
    {
        var culture = ResolveCulture(context);
        if (culture is null)
            return error.Message;

        var current = settings.CurrentValue;
        if (error.Metadata?.TryGetValue(MessageKeyMetadata, out var key) == true
            && key is string messageKey
            && TryCatalog(current, culture, messageKey, out var keyed)
            && MessageTemplateMapper.TryFormat(keyed, error.Metadata, culture, out var keyedMessage))
            return keyedMessage;

        if (!current.TranslateByErrorType)
            return error.Message;

        var type = error.Type.ToString();
        return (TryCatalog(current, culture, type, out var template) || TryBuiltIn(ErrorTypeMessageConstants.Messages, culture, type, out template))
            && MessageTemplateMapper.TryFormat(template, error.Metadata, culture, out var message)
                ? message
                : error.Message;
    }

    private string TranslateCore(HttpContext context, FieldError error)
    {
        var culture = ResolveCulture(context);
        if (culture is null || string.IsNullOrEmpty(error.Code))
            return error.Message;

        var current = settings.CurrentValue;
        var arguments = error.Params?.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.Ordinal);
        return (TryCatalog(current, culture, error.Code, out var template)
                || TryBuiltIn(FieldCodeMessageConstants.Messages, culture, error.Code, out template)
                || (current.UseValidatorTranslations && TryValidatorPack(culture, error.Code, out template)))
            && MessageTemplateMapper.TryFormat(template, arguments, culture, out var message)
                ? message
                : error.Message;
    }

    /// <summary>Pseudo-localizes <paramref name="message"/> while both translation and pseudo-localization are on.</summary>
    private string Pseudo(string message)
    {
        var current = settings.CurrentValue;
        return current.Enabled && current.PseudoLocalization ? MessageTemplateMapper.Pseudo(message) : message;
    }

    private static CultureInfo? Resolve(HttpContext context, ErrorTranslationSettings current)
    {
        foreach (var requested in Requested(context))
        {
            if (IsWithin(requested, current.DefaultCulture))
                return null;
            if (current.SupportedCultures.Count == 0 || current.SupportedCultures.Exists(supported => IsWithin(requested, supported) || IsWithin(Culture(supported), requested.Name)))
                return requested;
        }

        return null;
    }

    private static IEnumerable<CultureInfo> Requested(HttpContext context)
    {
        if (context.Features.Get<IRequestCultureFeature>() is { } feature)
        {
            yield return feature.RequestCulture.UICulture;
            yield break;
        }

        var languages = context.Request.GetTypedHeaders().AcceptLanguage;
        foreach (var language in languages.OrderByDescending(entry => entry.Quality ?? 1))
        {
            if (language.Value.Value is { } tag && tag != "*" && Culture(tag) is { } culture)
                yield return culture;
        }
    }

    private static CultureInfo? Culture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="culture"/> is <paramref name="name"/> or one of its sub-cultures.</summary>
    private static bool IsWithin(CultureInfo? culture, string name)
    {
        for (var current = culture; current is not null && !Equals(current, CultureInfo.InvariantCulture); current = current.Parent)
        {
            if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryCatalog(ErrorTranslationSettings current, CultureInfo culture, string key, [NotNullWhen(true)] out string? template)
    {
        for (var candidate = culture; !Equals(candidate, CultureInfo.InvariantCulture); candidate = candidate.Parent)
        {
            if (current.Messages.TryGetValue(candidate.Name, out var catalog))
            {
                if (catalog.TryGetValue(key, out template))
                    return true;

                foreach (var (entryKey, value) in catalog)
                {
                    if (string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        template = value;
                        return true;
                    }
                }
            }
        }

        template = null;
        return false;
    }

    private static bool TryBuiltIn(
        FrozenDictionary<string, FrozenDictionary<string, string>> builtIn,
        CultureInfo culture,
        string key,
        [NotNullWhen(true)] out string? template)
    {
        for (var candidate = culture; !Equals(candidate, CultureInfo.InvariantCulture); candidate = candidate.Parent)
        {
            if (builtIn.TryGetValue(candidate.Name, out var messages) && messages.TryGetValue(key, out template))
                return true;
        }

        template = null;
        return false;
    }

    /// <summary>FluentValidation's text for a validator code in the culture, or a specific sub-culture (Latin script first).</summary>
    private bool TryValidatorPack(CultureInfo culture, string code, [NotNullWhen(true)] out string? template)
    {
        var languages = ValidatorOptions.Global.LanguageManager;
        var english = languages.GetString(code, English);
        if (!string.IsNullOrEmpty(english))
        {
            foreach (var candidate in _validatorCultures.GetOrAdd(culture.Name, _ => ValidatorCandidates(culture)))
            {
                var translated = languages.GetString(code, candidate);
                if (!string.IsNullOrEmpty(translated) && !string.Equals(translated, english, StringComparison.Ordinal))
                {
                    template = translated;
                    return true;
                }
            }
        }

        template = null;
        return false;
    }

    private static CultureInfo[] ValidatorCandidates(CultureInfo culture)
        => culture.IsNeutralCulture
            ? [culture, .. CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Where(specific => IsWithin(specific, culture.Name))
                .OrderByDescending(specific => specific.Name.Contains("-Latn", StringComparison.Ordinal))
                .ThenBy(specific => specific.Name, StringComparer.Ordinal)]
            : [culture];
}
