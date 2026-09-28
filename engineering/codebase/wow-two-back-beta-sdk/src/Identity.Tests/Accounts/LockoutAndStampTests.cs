using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Cookies;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SecurityStamps;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Lockout windows, and security-stamp revocation of cookie and bearer principals through a real pipeline.</summary>
public sealed class LockoutAndStampTests
{
    private const string JwtKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Lockout_ShouldLockOnTheMaximumAttemptAndExpireWithTheWindow()
    {
        await using var host = AccountsHost.Create(identity => identity.AddLockout(), o => o.Lockout.MaxFailedAttempts = 3);
        var user = await host.CreateUserAsync("ivan");
        await using var scope = host.Scope();
        var lockout = scope.ServiceProvider.GetRequiredService<UserLockoutService<IdentityUser, Guid>>();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!;

        (await lockout.RecordFailedAttemptAsync(tracked)).Should().BeFalse();
        (await lockout.RecordFailedAttemptAsync(tracked)).Should().BeFalse();
        (await lockout.RecordFailedAttemptAsync(tracked)).Should().BeTrue();

        var stored = await host.ReloadAsync(user.Id);
        lockout.IsLockedOut(stored).Should().BeTrue();
        stored.AccessFailedCount.Should().Be(0);
        stored.LockoutEnd.Should().Be(host.Time.GetUtcNow() + TimeSpan.FromMinutes(5));

        host.Time.Advance(TimeSpan.FromMinutes(5));
        lockout.IsLockedOut(stored).Should().BeFalse();
    }

    [Fact]
    public async Task Lockout_ShouldNeverCountAUserWithLockoutDisabled()
    {
        await using var host = AccountsHost.Create(identity => identity.AddLockout(), o =>
        {
            o.Lockout.MaxFailedAttempts = 1;
            o.Lockout.EnabledForNewUsers = false;
        });
        var user = await host.CreateUserAsync("jane");
        await using var scope = host.Scope();
        var lockout = scope.ServiceProvider.GetRequiredService<UserLockoutService<IdentityUser, Guid>>();

        (await lockout.RecordFailedAttemptAsync(user)).Should().BeFalse();
        lockout.IsLockedOut(user).Should().BeFalse();
        (await lockout.LockUntilAsync(user, host.Time.GetUtcNow().AddDays(1))).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.LockoutNotEnabled);
    }

    [Fact]
    public async Task StampValidator_ShouldRejectAPrincipalAfterRotationAndPassForeignPrincipals()
    {
        await using var host = AccountsHost.Create(identity => identity.AddSecurityStampValidation(o => o.ValidationInterval = TimeSpan.Zero));
        var user = await host.CreateUserAsync("kate");
        await using var scope = host.Scope();
        var validator = scope.ServiceProvider.GetRequiredService<SecurityStampValidator<IdentityUser, Guid>>();
        var principal = PrincipalOf(user);

        (await validator.ValidateAsync(principal)).Should().BeTrue();
        (await validator.ValidateAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", "api")], "ApiKey")))).Should().BeTrue();
        (await validator.ValidateAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid"), new Claim("AspNet.Identity.SecurityStamp", "x")], "Cookies"))))
            .Should().BeFalse();

        await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>()
            .RotateSecurityStampAsync((await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!);
        (await validator.ValidateAsync(principal)).Should().BeFalse();
    }

    [Fact]
    public async Task CookiePrincipal_ShouldBeSignedOut_WhenTheStampRotates()
    {
        await using var app = await StartAsync(services => services.AddCookieAuthentication(o => o.Mode = AuthChallengeMode.Api), web =>
            web.MapPost("/login/{id:guid}", async (Guid id, HttpContext http, UserAccountService<IdentityUser, Guid> accounts) =>
            {
                var user = await accounts.FindByIdAsync(id);
                await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, PrincipalOf(user!));
                return Results.Ok();
            }));
        var user = await app.CreateUserAsync("liam");
        var client = app.Server.CreateClient();

        var login = await client.PostAsync($"/login/{user.Id}", null);
        var cookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        (await client.GetAsync("/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        await app.RotateAsync(user.Id);
        (await client.GetAsync("/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BearerPrincipal_ShouldBeRejected_WhenTheStampRotates()
    {
        await using var app = await StartAsync(services =>
        {
            services.AddJwtBearerAuthentication(o => { o.Issuer = "tests"; o.Audience = "tests"; o.SymmetricKey = JwtKey; });
            services.AddJwtTokenIssuance(o => { o.Issuer = "tests"; o.Audience = "tests"; o.SigningKey = JwtKey; });
        });
        var user = await app.CreateUserAsync("mona");
        var token = app.Server.Services.GetRequiredService<ITokenIssuer>().Issue(PrincipalOf(user).Claims);
        var client = app.Server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync("/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        await app.RotateAsync(user.Id);
        (await client.GetAsync("/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static ClaimsPrincipal PrincipalOf(IdentityUser user) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim("AspNet.Identity.SecurityStamp", user.SecurityStamp!)],
        "Tests"));

    private static async Task<PipelineApp> StartAsync(Action<IServiceCollection> authentication, Action<WebApplication>? endpoints = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AccountsDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddUserAccounts<IdentityUser>()
            .AddEntityFrameworkStores<AccountsDbContext>()
            .AddSecurityStampValidation(o => o.ValidationInterval = TimeSpan.Zero);
        authentication(builder.Services);
        builder.Services.AddAuthorization();

        var web = builder.Build();
        web.UseAuthentication();
        web.UseAuthorization();
        web.MapGet("/me", () => Results.Ok()).RequireAuthorization();
        endpoints?.Invoke(web);
        await web.StartAsync();

        using (var scope = web.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.EnsureCreated();

        return new PipelineApp(web, connection);
    }

    private sealed class PipelineApp(WebApplication web, SqliteConnection connection) : IAsyncDisposable
    {
        public TestServer Server => web.GetTestServer();

        public async Task<IdentityUser> CreateUserAsync(string userName)
        {
            await using var scope = web.Services.CreateAsyncScope();
            var user = new IdentityUser { UserName = userName, Email = $"{userName}@example.test" };
            await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().CreateAsync(user);
            return user;
        }

        public async Task RotateAsync(Guid id)
        {
            await using var scope = web.Services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();
            await accounts.RotateSecurityStampAsync((await accounts.FindByIdAsync(id))!);
        }

        public async ValueTask DisposeAsync()
        {
            await web.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
