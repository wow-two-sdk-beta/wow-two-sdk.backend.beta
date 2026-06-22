using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;

/// <summary>Marks a request for authorization via ASP.NET Core's <see cref="IAuthorizationService"/>.</summary>
public interface IRequireAuthorization
{
    /// <summary>Optional policy name. If <c>null</c>, default policy is used.</summary>
    string? PolicyName => null;
}

/// <summary>Runs ASP.NET Core authorization on requests implementing <see cref="IRequireAuthorization"/>.</summary>
/// <remarks>Throws <see cref="UnauthorizedAccessException"/> if not authenticated, <see cref="AuthorizationException"/> if not authorized.</remarks>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorizationService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    /// <param name="request">The request flowing through the pipeline.</param>
    /// <param name="nextStep">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> nextStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);

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

        return await nextStep().ConfigureAwait(false);
    }
}

/// <summary>Thrown when the user is authenticated but lacks permission.</summary>
/// <param name="failure">The underlying authorization failure, if any.</param>
public sealed class AuthorizationException(AuthorizationFailure? failure) : Exception("Forbidden")
{
    /// <summary>The underlying authorization failure, if any.</summary>
    public AuthorizationFailure? Failure { get; } = failure;
}

/// <summary>Registration helper.</summary>
public static class AuthorizationBehaviorServiceCollectionExtensions
{
    /// <summary>Register the authorization pipeline behavior. Requires <c>AddHttpContextAccessor()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddMediatorAuthorizationBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.AddAuthorization();
        return services.AddMediatorBehavior(typeof(AuthorizationBehavior<,>));
    }
}
