using System.Text.RegularExpressions;
using Microsoft.Net.Http.Headers;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;

namespace WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Parsers;

/// <summary>Parses Accept-Language into weighted basic language ranges.</summary>
public sealed partial class AcceptLanguageParser : IAcceptLanguageParser
{
    /// <summary>Parses all preferences or returns a validation failure; empty input yields an empty list.</summary>
    public Result<IReadOnlyList<LanguagePreferenceModel>> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Result<IReadOnlyList<LanguagePreferenceModel>>.Ok([]);
        if (value.Length > 8192 || !StringWithQualityHeaderValue.TryParseStrictList([value], out var parsed) || parsed.Count > 64)
            return Invalid();
        var items = new List<LanguagePreferenceModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in parsed)
        {
            var range = item.Value.ToString().ToLowerInvariant();
            if (!BasicRange().IsMatch(range) || !seen.Add(range)) return Invalid();
            items.Add(new LanguagePreferenceModel { Range = range, Quality = item.Quality ?? 1 });
        }
        return Result<IReadOnlyList<LanguagePreferenceModel>>.Ok(items.OrderByDescending(x => x.Quality).ToArray());
    }

    private static Result<IReadOnlyList<LanguagePreferenceModel>> Invalid() =>
        Result<IReadOnlyList<LanguagePreferenceModel>>.Fail(ValidationError.From(
            [new FieldError { Property = "Accept-Language", Message = "Use valid, distinct language ranges and quality values.", Code = "AcceptLanguageSyntax" }]));

    [GeneratedRegex(@"\A(?:\*|[a-z]{1,8}(?:-[a-z0-9]{1,8})*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex BasicRange();
}
