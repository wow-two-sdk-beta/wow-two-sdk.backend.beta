using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;

/// <summary>
/// Marker — requests implementing this interface are authorized via ASP.NET Core's <see cref="IAuthorizationService"/>.
/// </summary>
public interface IRequireAuthorization
{
    /// <summary>Optional policy name. If <c>null</c>, default policy is used.</summary>
    string? PolicyName => null;
}

/// <summary>
/// Pipeline behavior — runs ASP.NET Core authorization on requests implementing <see cref="IRequireAuthorization"/>.
/// Throws <see cref="UnauthorizedAccessException"/> if not authenticated, <see cref="AuthorizationException"/> if not authorized.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is IRequireAuthorization authReq)
        {
            var ctx = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("AuthorizationBehavior requires HttpContext (IHttpContextAccessor returned null).");

            if (ctx.User?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException("Request requires authentication.");

            var policy = authReq.PolicyName;
            var result = policy is null
                ? await authorizationService.AuthorizeAsync(ctx.User, request, Array.Empty<IAuthorizationRequirement>()).ConfigureAwait(false)
                : await authorizationService.AuthorizeAsync(ctx.User, request, policy).ConfigureAwait(false);

            if (!result.Succeeded)
                throw new AuthorizationException(result.Failure);
        }

        return await next().ConfigureAwait(false);
    }
}

/// <summary>Thrown when the user is authenticated but lacks permission.</summary>
public sealed class AuthorizationException(AuthorizationFailure? failure) : Exception("Forbidden")
{
    /// <summary>The underlying authorization failure, if any.</summary>
    public AuthorizationFailure? Failure { get; } = failure;
}

/// <summary>Registration helper.</summary>
public static class AuthorizationBehaviorServiceCollectionExtensions
{
    /// <summary>Register the authorization pipeline behavior. Requires <c>AddHttpContextAccessor()</c>.</summary>
    public static IServiceCollection AddMediatorAuthorizationBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.AddAuthorization();
        return services.AddMediatorBehavior(typeof(AuthorizationBehavior<,>));
    }
}
