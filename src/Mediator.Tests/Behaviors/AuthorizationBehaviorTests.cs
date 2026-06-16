using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> — requests marked <see cref="IRequireAuthorization"/>
/// are gated by ASP.NET Core authorization: unmarked passes through, unauthenticated throws
/// <see cref="UnauthorizedAccessException"/>, authenticated-but-denied throws <see cref="AuthorizationException"/>.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    private sealed record Secured : IRequest<string>, IRequireAuthorization;

    private sealed record SecuredWithPolicy(string? PolicyName) : IRequest<string>, IRequireAuthorization;

    private sealed record Open : IRequest<string>; // not marked

    // Build the auth service with an "admin" policy that requires the admin role.
    private static IAuthorizationService BuildAuthService()
        => new ServiceCollection()
            .AddAuthorization(o => o.AddPolicy("admin", p => p.RequireRole("admin")))
            .AddLogging()
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();

    private static HttpContextAccessor Accessor(ClaimsPrincipal? user)
    {
        var ctx = new DefaultHttpContext();
        if (user is not null) ctx.User = user;
        return new HttpContextAccessor { HttpContext = ctx };
    }

    private static ClaimsPrincipal Authenticated(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static AuthorizationBehavior<TReq, string> Behavior<TReq>(ClaimsPrincipal? user)
        where TReq : notnull
        => new(Accessor(user), BuildAuthService());

    [Fact]
    public async Task Unmarked_request_passes_through_without_auth()
    {
        // No HttpContext at all → still fine because the request isn't IRequireAuthorization.
        var behavior = new AuthorizationBehavior<Open, string>(new HttpContextAccessor(), BuildAuthService());

        var result = await behavior.HandleAsync(new Open(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Authenticated_user_with_no_policy_is_denied_empty_requirements_path()
    {
        // ROUGH EDGE: with PolicyName == null the behavior calls AuthorizeAsync(user, resource, <empty requirements>).
        // An empty requirement set yields a non-succeeded result, so even an authenticated user is forbidden.
        // (To authorize an authenticated user the request must name a policy, or the no-policy path needs a
        //  RequireAuthenticatedUser requirement instead of an empty array. Documented here as observed behavior.)
        var behavior = Behavior<Secured>(Authenticated());

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task Unauthenticated_user_throws_unauthorized()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated == false
        var behavior = Behavior<Secured>(anonymous);

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Missing_http_context_throws_invalid_operation()
    {
        var behavior = new AuthorizationBehavior<Secured, string>(new HttpContextAccessor { HttpContext = null }, BuildAuthService());

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Authenticated_but_policy_denied_throws_authorization_exception()
    {
        // Policy "admin" requires the admin role; this user has only "user".
        var behavior = Behavior<SecuredWithPolicy>(Authenticated("user"));

        var act = async () => await behavior.HandleAsync(
            new SecuredWithPolicy("admin"),
            () => ValueTask.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<AuthorizationException>();
    }

    [Fact]
    public async Task Authenticated_and_policy_satisfied_passes()
    {
        var behavior = Behavior<SecuredWithPolicy>(Authenticated("admin"));

        var result = await behavior.HandleAsync(
            new SecuredWithPolicy("admin"),
            () => ValueTask.FromResult("granted"),
            CancellationToken.None);

        result.Should().Be("granted");
    }
}
