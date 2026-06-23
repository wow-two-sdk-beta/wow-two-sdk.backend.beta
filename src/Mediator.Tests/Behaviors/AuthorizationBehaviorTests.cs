using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Mediator.Authorization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Tests.Behaviors;

/// <summary>
/// <see cref="AuthorizationBehavior{TRequest,TResponse}"/> — requests marked <see cref="IRequireAuthorization"/>
/// are gated by ASP.NET Core authorization: unmarked passes through, unauthenticated throws an
/// <see cref="AppException"/> (Unauthorized), authenticated-but-denied throws an <see cref="AppException"/> (Forbidden).
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
    public async Task HandleAsync_ShouldPassThroughWithoutAuth_WhenRequestUnmarked()
    {
        // No HttpContext at all → still fine because the request isn't IRequireAuthorization.
        var behavior = new AuthorizationBehavior<Open, string>(new HttpContextAccessor(), BuildAuthService());

        var result = await behavior.HandleAsync(new Open(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowForbidden_WhenAuthenticatedUserHasNoPolicy()
    {
        // ROUGH EDGE: with PolicyName == null the behavior calls AuthorizeAsync(user, resource, <empty requirements>).
        // An empty requirement set yields a non-succeeded result, so even an authenticated user is forbidden.
        // (To authorize an authenticated user the request must name a policy, or the no-policy path needs a
        //  RequireAuthenticatedUser requirement instead of an empty array. Documented here as observed behavior.)
        var behavior = Behavior<Secured>(Authenticated());

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowUnauthorized_WhenUserUnauthenticated()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity()); // IsAuthenticated == false
        var behavior = Behavior<Secured>(anonymous);

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Unauthorized);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowInvalidOperation_WhenHttpContextMissing()
    {
        var behavior = new AuthorizationBehavior<Secured, string>(new HttpContextAccessor { HttpContext = null }, BuildAuthService());

        var act = async () => await behavior.HandleAsync(new Secured(), () => ValueTask.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowForbidden_WhenPolicyDenied()
    {
        // Policy "admin" requires the admin role; this user has only "user".
        var behavior = Behavior<SecuredWithPolicy>(Authenticated("user"));

        var act = async () => await behavior.HandleAsync(
            new SecuredWithPolicy("admin"),
            () => ValueTask.FromResult("ok"),
            CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_ShouldPass_WhenAuthenticatedAndPolicySatisfied()
    {
        var behavior = Behavior<SecuredWithPolicy>(Authenticated("admin"));

        var result = await behavior.HandleAsync(
            new SecuredWithPolicy("admin"),
            () => ValueTask.FromResult("granted"),
            CancellationToken.None);

        result.Should().Be("granted");
    }
}
