using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Emails;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.EmailSignIn;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;
using WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Email;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;
using WoW.Two.Sdk.Backend.Beta.Meta;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>The account HTTP API end to end: registration, email confirmation, bearer sessions, refresh and password reset.</summary>
public sealed partial class AccountEndpointTests
{
    private const string JwtKey = "0123456789abcdef0123456789abcdef";
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Account_LifecycleRunsThroughTheApi()
    {
        await using var app = await StartAsync();
        var client = app.Server.CreateClient();

        using var registered = await client.PostAsJsonAsync("/account/register", new { email = "zoe@example.test", password = Password });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Data(registered)).GetProperty("confirmationSent").GetBoolean().Should().BeTrue();

        var confirmation = app.Mail.Sent.Single(message => message.Subject == "Confirm your email");
        var link = LinkPattern().Match(confirmation.TextBody!).Value;
        (await client.GetAsync("/account/confirm-email" + new Uri(link).Query)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var login = await client.PostAsJsonAsync("/account/login", new { login = "zoe@example.test", password = Password });
        var session = await Data(login);
        var access = session.GetProperty("accessToken").GetString();
        var refresh = session.GetProperty("refreshToken").GetString();

        using var info = new HttpRequestMessage(HttpMethod.Get, "/account/manage/info");
        info.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var account = await Data(await client.SendAsync(info));
        account.GetProperty("email").GetString().Should().Be("zoe@example.test");
        account.GetProperty("emailConfirmed").GetBoolean().Should().BeTrue();

        using var renewed = await client.PostAsJsonAsync("/account/refresh", new { refreshToken = refresh });
        (await Data(renewed)).GetProperty("refreshToken").GetString().Should().NotBe(refresh);
        using var replayed = await client.PostAsJsonAsync("/account/refresh", new { refreshToken = refresh });
        replayed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/account/forgot-password", new { email = "zoe@example.test" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reset = app.Mail.Sent.Single(message => message.Subject == "Reset your password");
        var resetToken = System.Web.HttpUtility.ParseQueryString(new Uri(LinkPattern().Match(reset.TextBody!).Value).Query)["token"];
        (await client.PostAsJsonAsync("/account/reset-password", new { email = "zoe@example.test", resetToken, newPassword = "a brand new passphrase" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/account/login", new { login = "zoe@example.test", password = "a brand new passphrase" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Failures_SurfaceAsProblemDetailsWithIdentityCodes()
    {
        await using var app = await StartAsync();
        var client = app.Server.CreateClient();
        await client.PostAsJsonAsync("/account/register", new { email = "yan@example.test", password = Password });

        using var duplicate = await client.PostAsJsonAsync("/account/register", new { email = "YAN@example.test", password = Password });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Problem(duplicate)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(IdentityErrorCodeConstants.DuplicateUserName);

        using var weak = await client.PostAsJsonAsync("/account/register", new { email = "new@example.test", password = "short" });
        (await Problem(weak)).GetProperty("errors")[0].GetProperty("property").GetString().Should().Be("password");

        using var wrong = await client.PostAsJsonAsync("/account/login", new { login = "yan@example.test", password = "wrong password" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/account/forgot-password", new { email = "nobody@example.test" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/account/manage/info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TwoFactor_IsManagedAndThenRequiredAtSignIn()
    {
        await using var app = await StartAsync();
        var client = app.Server.CreateClient();
        await client.PostAsJsonAsync("/account/register", new { email = "xia@example.test", password = Password });
        var access = (await Data(await client.PostAsJsonAsync("/account/login", new { login = "xia@example.test", password = Password })))
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var setup = await Data(await client.PostAsync("/account/manage/2fa/authenticator", null));
        setup.GetProperty("authenticatorUri").GetString().Should().StartWith("otpauth://totp/");
        var totp = app.Services.GetRequiredService<ITotpService>();
        var key = OtpNet.Base32Encoding.ToBytes(setup.GetProperty("sharedKey").GetString());

        var enabled = await Data(await client.PostAsJsonAsync("/account/manage/2fa/enable", new { code = totp.ComputeCode(key) }));
        enabled.GetProperty("recoveryCodes").GetArrayLength().Should().Be(10);
        var status = await Data(await client.GetAsync("/account/manage/2fa"));
        status.GetProperty("enabled").GetBoolean().Should().BeTrue();

        client.DefaultRequestHeaders.Authorization = null;
        using var withoutCode = await client.PostAsJsonAsync("/account/login", new { login = "xia@example.test", password = Password });
        withoutCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Problem(withoutCode)).GetProperty("detail").GetString().Should().Be("Enter your two-factor code.");

        using var withCode = await client.PostAsJsonAsync("/account/login", new { login = "xia@example.test", password = Password, twoFactorCode = totp.ComputeCode(key) });
        withCode.StatusCode.Should().Be(HttpStatusCode.OK);
        using var withRecovery = await client.PostAsJsonAsync("/account/login", new { login = "xia@example.test", password = Password, recoveryCode = enabled.GetProperty("recoveryCodes")[0].GetString() });
        withRecovery.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeliveredTwoFactor_ConfiguredByTheHost_CompletesLoginOverEmail()
    {
        await using var app = await StartAsync(new Dictionary<string, string?>
        {
            ["Identity:TwoFactor:Methods:email:Channel"] = "email",
            ["Identity:TwoFactor:Methods:email:Code:Kind"] = "Alphanumeric",
            ["Identity:TwoFactor:Methods:email:Code:Length"] = "8",
            ["Identity:Otp:RateLimitWindow"] = "00:00:00",
            ["Identity:Otp:Messages:AppName"] = "Tests",
        });
        var client = app.Server.CreateClient();
        await client.PostAsJsonAsync("/account/register", new { email = "ida@example.test", password = Password });
        var confirmation = app.Mail.Sent.Single(message => message.Subject == "Confirm your email");
        await client.GetAsync("/account/confirm-email" + new Uri(LinkPattern().Match(confirmation.TextBody!).Value).Query);
        var access = (await Data(await client.PostAsJsonAsync("/account/login", new { login = "ida@example.test", password = Password })))
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        (await Data(await client.GetAsync("/account/manage/2fa"))).GetProperty("methods").EnumerateArray().Select(m => m.GetString()).Should().Equal("email");
        var sent = await Data(await client.PostAsync("/account/manage/2fa/methods/email/send", null));
        sent.GetProperty("method").GetString().Should().Be("email");
        var setupCode = CodeOf(app.Mail.Sent.Last());
        app.Mail.Sent.Last().Subject.Should().Be("Your Tests verification code");
        setupCode.Should().MatchRegex("^[A-HJ-NP-Z2-9]{8}$");
        (await Data(await client.PostAsJsonAsync("/account/manage/2fa/methods/email/enable", new { code = setupCode })))
            .GetProperty("recoveryCodes").GetArrayLength().Should().Be(10);
        (await Data(await client.GetAsync("/account/manage/2fa"))).GetProperty("preferredMethod").GetString().Should().Be("email");

        client.DefaultRequestHeaders.Authorization = null;
        using var challenge = await client.PostAsJsonAsync("/account/login", new { login = "ida@example.test", password = Password });
        challenge.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await Problem(challenge);
        problem.GetProperty("twoFactorMethod").GetString().Should().Be("email");
        problem.GetProperty("codeSent").GetBoolean().Should().BeTrue();
        problem.GetProperty("twoFactorMethods").EnumerateArray().Select(m => m.GetString()).Should().Equal("email");

        var loginCode = CodeOf(app.Mail.Sent.Last());
        using var wrong = await client.PostAsJsonAsync("/account/login", new { login = "ida@example.test", password = Password, twoFactorCode = "WRONG123" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var withCode = await client.PostAsJsonAsync("/account/login", new { login = "ida@example.test", password = Password, twoFactorCode = loginCode.ToLowerInvariant() });
        withCode.StatusCode.Should().Be(HttpStatusCode.OK);

        using var unknown = await client.PostAsJsonAsync("/account/login", new { login = "ida@example.test", password = Password, twoFactorMethod = "sms" });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Problem(unknown)).GetProperty("detail").GetString().Should().Contain("'sms'");
    }

    [Fact]
    public async Task Passkeys_AreManagedAndSignInThroughTheApi()
    {
        await using var app = await StartAsync();
        var client = app.Server.CreateClient();
        using var authenticator = new SoftwareAuthenticator();
        await client.PostAsJsonAsync("/account/register", new { email = "kai@example.test", password = Password });
        var access = (await Data(await client.PostAsJsonAsync("/account/login", new { login = "kai@example.test", password = Password })))
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var creation = await Data(await client.PostAsync("/account/manage/passkeys/options", null));
        creation.GetProperty("options").GetProperty("rp").GetProperty("id").GetString().Should().Be("app.test");
        var credential = JsonDocument.Parse(authenticator.Create(creation.GetProperty("options").GetRawText())).RootElement;
        using var registered = await client.PostAsJsonAsync("/account/manage/passkeys", new { state = creation.GetProperty("state").GetString(), credential, name = "Phone" });
        registered.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var passkey = (await Data(await client.GetAsync("/account/manage/passkeys"))).EnumerateArray().Should().ContainSingle().Subject;
        passkey.GetProperty("name").GetString().Should().Be("Phone");

        client.DefaultRequestHeaders.Authorization = null;
        var request = await Data(await client.PostAsync("/account/passkeys/login/options", null));
        var body = new
        {
            state = request.GetProperty("state").GetString(),
            credential = JsonDocument.Parse(authenticator.Get(request.GetProperty("options").GetRawText())).RootElement,
        };
        var session = await Data(await client.PostAsJsonAsync("/account/passkeys/login", body));
        session.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();

        using var replayed = await client.PostAsJsonAsync("/account/passkeys/login", body);
        replayed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Problem(replayed)).GetProperty("detail").GetString().Should().Be("The passkey could not be verified; try again.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        var id = passkey.GetProperty("id").GetGuid();
        (await client.DeleteAsync($"/account/manage/passkeys/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/account/manage/passkeys/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmailSignIn_ShouldUseLinksAndCodesOnce_AndHandOffToTwoFactor()
    {
        await using var app = await StartAsync(new Dictionary<string, string?>
        {
            ["UserAccounts:Emails:MagicLink"] = "https://app.test/magic?email={email}&token={token}",
            ["Identity:Otp:RateLimitWindow"] = "00:00:00",
        });
        var client = app.Server.CreateClient();
        await client.PostAsJsonAsync("/account/register", new { email = "lea@example.test", password = Password });

        (await client.PostAsJsonAsync("/account/email-sign-in/send", new { email = "nobody@example.test" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/account/email-sign-in/send", new { email = "LEA@example.test" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        app.Mail.Sent.Should().NotContain(message => message.To[0].Address == "nobody@example.test");
        var token = System.Web.HttpUtility.ParseQueryString(new Uri(LinkPattern().Match(app.Mail.Sent.Single(message => message.Subject == "Your sign-in link").TextBody!).Value).Query)["token"];

        using var linked = await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", token });
        var access = (await Data(linked)).GetProperty("accessToken").GetString();
        using var info = new HttpRequestMessage(HttpMethod.Get, "/account/manage/info");
        info.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        (await Data(await client.SendAsync(info))).GetProperty("emailConfirmed").GetBoolean().Should().BeTrue("the link proved the address");

        using var replayed = await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", token });
        replayed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Problem(replayed)).GetProperty("detail").GetString().Should().Be("The link or code is invalid or has expired.");

        await client.PostAsJsonAsync("/account/email-sign-in/send", new { email = "lea@example.test", method = "code" });
        var code = app.Mail.Sent.Last(message => message.Subject == "Your sign-in code").TextBody!.Split(' ')[4].TrimEnd('.');
        code.Should().MatchRegex("^[0-9]{6}$");
        (await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", code = "000000" == code ? "111111" : "000000" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", code })).StatusCode.Should().Be(HttpStatusCode.OK, "a wrong guess leaves the code usable");
        (await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", code })).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a used code is spent");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var setup = await Data(await client.PostAsync("/account/manage/2fa/authenticator", null));
        var totp = app.Services.GetRequiredService<ITotpService>();
        var key = OtpNet.Base32Encoding.ToBytes(setup.GetProperty("sharedKey").GetString());
        await client.PostAsJsonAsync("/account/manage/2fa/enable", new { code = totp.ComputeCode(key) });
        client.DefaultRequestHeaders.Authorization = null;

        await client.PostAsJsonAsync("/account/email-sign-in/send", new { email = "lea@example.test" });
        var second = System.Web.HttpUtility.ParseQueryString(new Uri(LinkPattern().Match(app.Mail.Sent.Last(message => message.Subject == "Your sign-in link").TextBody!).Value).Query)["token"];
        using var challenge = await client.PostAsJsonAsync("/account/email-sign-in", new { email = "lea@example.test", token = second });
        challenge.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await Problem(challenge);
        var ticket = problem.GetProperty("twoFactorTicket").GetString();
        var userId = problem.GetProperty("userId").GetString();

        (await client.PostAsJsonAsync("/account/login/two-factor", new { userId, ticket, twoFactorCode = "000000" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var completed = await client.PostAsJsonAsync("/account/login/two-factor", new { userId, ticket, twoFactorCode = totp.ComputeCode(key) });
        (await Data(completed)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    private static string CodeOf(EmailMessage message) => message.TextBody!.Split(' ')[0];

    [GeneratedRegex(@"https://app\.test/\S+")]
    private static partial Regex LinkPattern();

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<AccountApp> StartAsync(Dictionary<string, string?>? configuration = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var mail = new CapturingEmailBroker();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["UserAccounts:Emails:ConfirmationLink"] = "https://app.test/confirm?userId={userId}&token={token}",
            ["UserAccounts:Emails:ResetLink"] = "https://app.test/reset?email={email}&token={token}",
        });
        if (configuration is not null)
            builder.Configuration.AddInMemoryCollection(configuration);
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
            options.EnableRateLimiting = false;
        });
        builder.Services.AddDbContext<AccountsDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddUserAccounts<IdentityUser>()
            .AddEntityFrameworkStores<AccountsDbContext>()
            .AddArgon2Passwords()
            .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey)
            .AddEmailConfirmation()
            .AddLockout()
            .AddRefreshTokens()
            .AddTwoFactor(o => o.Issuer = "Tests")
            .AddEmailSignIn()
            .AddPasskeys(o =>
            {
                o.ServerDomain = "app.test";
                o.Origins.Add("https://app.test");
            })
            .AddSignIn()
            .AddAccountEndpoints();
        builder.Services.AddJwtBearerAuthentication(o => { o.Issuer = "tests"; o.Audience = "tests"; o.SymmetricKey = JwtKey; });
        builder.Services.AddJwtTokenIssuance(o => { o.Issuer = "tests"; o.Audience = "tests"; o.SigningKey = JwtKey; });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IEmailBroker>(mail);
        builder.Services.AddEmailOtpDelivery();

        var web = builder.Build();
        web.UseApiDefaults(pipeline =>
        {
            pipeline.UseAuthentication();
            pipeline.UseAuthorization();
        });
        web.MapGroup("/account").MapUserAccountEndpoints<IdentityUser>();
        await web.StartAsync();
        using (var scope = web.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.EnsureCreated();

        return new AccountApp(web, connection, mail);
    }

    private sealed class AccountApp(WebApplication web, SqliteConnection connection, CapturingEmailBroker mail) : IAsyncDisposable
    {
        public TestServer Server => web.GetTestServer();

        public CapturingEmailBroker Mail { get; } = mail;

        public IServiceProvider Services => web.Services;

        public async ValueTask DisposeAsync()
        {
            await web.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CapturingEmailBroker : IEmailBroker
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(new EmailSendResult { Success = true, ProviderMessageId = Guid.NewGuid().ToString() });
        }
    }
}
