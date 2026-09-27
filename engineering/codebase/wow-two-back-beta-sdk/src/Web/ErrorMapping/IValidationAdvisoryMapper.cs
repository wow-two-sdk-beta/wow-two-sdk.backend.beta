using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorMapping;

/// <summary>Defines mapping the current request's validation advisories to their display messages.</summary>
public interface IValidationAdvisoryMapper
{
    /// <summary>Maps each tracked warning and suggestion, resolving its message for <paramref name="context"/>.</summary>
    /// <param name="context">The current request, which selects the message culture.</param>
    /// <returns>The advisories in recording order; empty when none were raised.</returns>
    IReadOnlyList<FieldError> Map(HttpContext context);
}
