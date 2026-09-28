using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Defines sending a rendered template through the registered <see cref="IEmailBroker"/>.</summary>
public interface ITemplatedEmailService
{
    /// <summary>Renders <paramref name="template"/> for the recipient's culture and sends it with its text twin.</summary>
    /// <param name="to">The recipient.</param>
    /// <param name="template">The template name.</param>
    /// <param name="values">Values by placeholder name.</param>
    /// <param name="culture">The recipient's culture; null takes the default culture.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="EmailTemplateRejectedException">The template or a value it names is missing.</exception>
    Task<EmailSendResult> SendAsync(EmailAddress to, string template, IReadOnlyDictionary<string, object?> values, CultureInfo? culture = null, CancellationToken cancellationToken = default);
}
