using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Mediator.Result;

namespace WoW.Two.Sdk.Backend.Beta.Web.Results;

/// <summary>Provides the <see cref="FailureCategory"/> → HTTP status code mapping.</summary>
public static class FailureCategoryExtensions
{
    /// <summary>Maps <paramref name="category"/> to its conventional HTTP status code (unknown → 500).</summary>
    /// <param name="category">The failure category.</param>
    public static int ToStatusCode(this FailureCategory category)
    {
        return category switch
        {
            FailureCategory.Validation => StatusCodes.Status400BadRequest,
            FailureCategory.NotFound => StatusCodes.Status404NotFound,
            FailureCategory.Conflict => StatusCodes.Status409Conflict,
            FailureCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            FailureCategory.Forbidden => StatusCodes.Status403Forbidden,
            FailureCategory.PaymentRequired => StatusCodes.Status402PaymentRequired,
            FailureCategory.Unavailable => StatusCodes.Status503ServiceUnavailable,
            FailureCategory.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
