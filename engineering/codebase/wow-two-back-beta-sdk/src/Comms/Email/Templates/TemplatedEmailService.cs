using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Provides templated sending: render, then hand the HTML and text bodies to the broker.</summary>
/// <param name="renderer">Renders the template.</param>
/// <param name="broker">Sends the message.</param>
public sealed class TemplatedEmailService(IEmailTemplateRenderer renderer, IEmailBroker broker) : ITemplatedEmailService
{
    /// <inheritdoc />
    public Task<EmailSendResult> SendAsync(EmailAddress to, string template, IReadOnlyDictionary<string, object?> values, CultureInfo? culture = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(to);
        var rendered = renderer.Render(template, values, culture);
        return broker.SendAsync(new EmailMessage { To = [to], Subject = rendered.Subject, HtmlBody = rendered.Html, TextBody = rendered.Text }, cancellationToken);
    }
}
