using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Defines rendering a named email template for a culture.</summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders <paramref name="template"/> in <paramref name="culture"/>, else its parent culture, else the default one.
    /// Values fill <c>{Name}</c> and ICU plural placeholders; they are escaped, so they never become markup or links.
    /// </summary>
    /// <param name="template">The template name.</param>
    /// <param name="values">Values by placeholder name.</param>
    /// <param name="culture">The recipient's culture; null takes the default culture.</param>
    /// <exception cref="EmailTemplateRejectedException">The template or a value it names is missing.</exception>
    EmailTemplateResult Render(string template, IReadOnlyDictionary<string, object?> values, CultureInfo? culture = null);
}
