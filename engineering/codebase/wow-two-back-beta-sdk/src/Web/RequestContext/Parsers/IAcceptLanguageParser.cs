using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;

namespace WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Parsers;

/// <summary>Defines parsing of HTTP language preferences.</summary>
public interface IAcceptLanguageParser
{
    /// <summary>Parses weighted language ranges, retaining exclusions for caller-owned negotiation.</summary>
    Result<IReadOnlyList<LanguagePreferenceModel>> Parse(string? value);
}
