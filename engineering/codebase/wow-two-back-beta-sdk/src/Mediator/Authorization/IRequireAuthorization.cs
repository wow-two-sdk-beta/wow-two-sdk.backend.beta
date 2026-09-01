using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;

/// <summary>Marks a request for authorization via ASP.NET Core's <see cref="IAuthorizationService"/>.</summary>
public interface IRequireAuthorization
{
    /// <summary>Optional policy name. If <c>null</c>, default policy is used.</summary>
    string? PolicyName => null;
}
