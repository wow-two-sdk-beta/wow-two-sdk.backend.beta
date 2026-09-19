using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Tenancy;

/// <summary>Verifies authoritative resolution precedence and ambient lifetime.</summary>
public sealed class TenantResolutionTests
{
    [Fact]
    public async Task Middleware_ShouldPreferClaim_AndClearAmbientStateAfterFailure()
    {
        var options = new TenancyConventionOptions
        {
            UseClaim = true,
            UseHeader = true,
        };
        var resolver = new TenantIdService(options);
        var ambient = new AmbientTenantContext();
        var tenantStore = new InMemoryTenantRepository(
        [
            new TenantInfo { Id = "claim-tenant", Name = "Claim tenant" },
        ]);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(options.ClaimType, "claim-tenant")],
                "test")),
        };
        httpContext.Request.Headers[options.HeaderName] = "header-tenant";
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            ambient.TenantId.Should().Be("claim-tenant");
            throw new InvalidOperationException("probe");
        });

        var action = () => middleware.InvokeAsync(httpContext, resolver, ambient, tenantStore);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("probe");
        ambient.HasTenant.Should().BeFalse();
    }

    [Fact]
    public void Defaults_ShouldUseAuthenticatedClaimOnly()
    {
        var options = new TenancyConventionOptions();

        options.UseClaim.Should().BeTrue();
        options.UseHeader.Should().BeFalse();
        options.UseRoute.Should().BeFalse();
        options.UseSubdomain.Should().BeFalse();
    }
}
