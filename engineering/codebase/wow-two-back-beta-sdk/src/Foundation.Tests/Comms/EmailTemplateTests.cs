using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Comms;

/// <summary>Markdown email templates: branded HTML with inlined styles, text twins, culture fallback, escaping and refusals.</summary>
public sealed class EmailTemplateTests
{
    private const string Welcome = """
        # Welcome, {Name}!

        You have **{Count, plural, one {# free conversion} other {# free conversions}}** left today.

        [Confirm your email]({Link} "button")

        - Merge and split PDFs
        - Compress images

        Questions? Reply to this email or read the [guide](https://example.test/guide).
        """;

    [Fact]
    public void Render_ShouldBrandTheHtmlAndWriteLinksOutInTheText()
    {
        var renderer = Build();

        var result = renderer.Render("welcome", Values("Ada", 1), CultureInfo.GetCultureInfo("en-GB"));

        result.Culture.Should().Be("en");
        result.Subject.Should().Be("Welcome to Tools, Ada");
        result.Html.Should().Contain("""<h1 style="margin:0 0 16px 0;font-size:24px;line-height:1.3;">Welcome, Ada!</h1>""")
            .And.Contain("<strong>1 free conversion</strong>")
            .And.Contain("""<td style="border-radius:6px;background:#0F766E;"><a style="display:inline-block;padding:12px 24px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;" href="https://example.test/confirm?t=abc">Confirm your email</a>""")
            .And.Contain("""<a style="color:#0F766E;" href="https://example.test/guide">guide</a>""")
            .And.Contain("Tools Ltd, Tashkent")
            .And.Contain("Your code is inside");
        result.Text.Should().Contain("Welcome, Ada!")
            .And.Contain("Confirm your email (https://example.test/confirm?t=abc)")
            .And.Contain("- Merge and split PDFs\n- Compress images")
            .And.Contain("guide (https://example.test/guide)")
            .And.NotContain("**").And.NotContain("<");
    }

    [Fact]
    public void Values_ShouldNeverBecomeMarkupOrLinks()
    {
        var result = Build().Render("welcome", Values("[Click me](https://evil.test) <script>alert(1)</script>", 3));

        result.Html.Should().NotContain("href=\"https://evil.test\"").And.NotContain("<script>");
        result.Html.Should().Contain("[Click me](https://evil.test) &lt;script&gt;alert(1)&lt;/script&gt;");
        result.Text.Should().Contain("Welcome, [Click me](https://evil.test) <script>alert(1)</script>!");
        result.Html.Should().Contain("<strong>3 free conversions</strong>");
    }

    [Fact]
    public void Cultures_ShouldFallBackThroughParentsToTheDefault()
    {
        var renderer = Build();

        renderer.Render("welcome", Values("Ada", 2), CultureInfo.GetCultureInfo("ru-RU")).Subject.Should().Be("Добро пожаловать, Ada");
        renderer.Render("welcome", Values("Ada", 2), CultureInfo.GetCultureInfo("de-DE")).Culture.Should().Be("en");
        renderer.Render("welcome", Values("Ada", 2)).Culture.Should().Be("en");
    }

    [Fact]
    public void MissingTemplatesOrValues_ShouldBeRefused()
    {
        var renderer = Build();

        FluentActions.Invoking(() => renderer.Render("farewell", Values("Ada", 1))).Should().Throw<EmailTemplateRejectedException>()
            .Which.Reason.Should().Be("email_template_missing");
        var missing = FluentActions.Invoking(() => renderer.Render("welcome", new Dictionary<string, object?> { ["Name"] = "Ada" })).Should().Throw<EmailTemplateRejectedException>().Which;
        missing.Reason.Should().Be("email_template_value_missing");
        missing.Message.Should().Contain("Count").And.Contain("Link");
    }

    [Fact]
    public async Task Send_ShouldHandBothBodiesToTheBroker()
    {
        var broker = new CapturingBroker();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailBroker>(broker);
        Configure(services);
        using var provider = services.BuildServiceProvider();

        var sent = await provider.GetRequiredService<ITemplatedEmailService>().SendAsync(new EmailAddress { Address = "ada@example.test" }, "welcome", Values("Ada", 1));

        sent.Success.Should().BeTrue();
        var message = broker.Sent.Single();
        message.To.Single().Address.Should().Be("ada@example.test");
        message.Subject.Should().Be("Welcome to Tools, Ada");
        message.HtmlBody.Should().StartWith("<!DOCTYPE html>");
        message.TextBody.Should().Contain("Confirm your email (https://example.test/confirm?t=abc)");
    }

    private static Dictionary<string, object?> Values(string name, int count)
        => new() { ["Name"] = name, ["Count"] = count, ["Link"] = "https://example.test/confirm?t=abc" };

    private static IEmailTemplateRenderer Build()
    {
        var services = new ServiceCollection();
        Configure(services);
        return services.BuildServiceProvider().GetRequiredService<IEmailTemplateRenderer>();
    }

    private static void Configure(IServiceCollection services) => services.AddEmailTemplates(o =>
    {
        o.Brand.Name = "Tools";
        o.Brand.AccentColor = "#0F766E";
        o.Brand.FooterText = "Tools Ltd, Tashkent";
        o.Templates["welcome"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new EmailTemplateContentOptions { Subject = "Welcome to Tools, {Name}", Body = Welcome, Preheader = "Your code is inside" },
            ["ru"] = new EmailTemplateContentOptions { Subject = "Добро пожаловать, {Name}", Body = "# Привет, {Name}! {Count} {Link}" },
        };
    });

    private sealed class CapturingBroker : IEmailBroker
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(new EmailSendResult { Success = true, ProviderMessageId = "1" });
        }
    }
}
