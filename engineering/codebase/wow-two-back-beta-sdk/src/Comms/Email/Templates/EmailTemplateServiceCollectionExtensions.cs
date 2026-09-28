using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>Registers Markdown email templates.</summary>
public static class EmailTemplateServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmailTemplateRenderer"/> and <see cref="ITemplatedEmailService"/> (which needs an
    /// <see cref="IEmailBroker"/>). Templates and brand come from <paramref name="configure"/>, then the host section
    /// <c>Comms:Email:Templates</c>, so copy edits and new languages need no rebuild.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Templates, brand and default culture.</param>
    public static IServiceCollection AddEmailTemplates(this IServiceCollection services, Action<EmailTemplateOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            EmailTemplateOptions.SectionName,
            configure,
            builder => builder.Validate(o => !string.IsNullOrWhiteSpace(o.DefaultCulture), "EmailTemplateOptions.DefaultCulture must not be blank."));
        services.TryAddSingleton<IEmailTemplateRenderer, MarkdownEmailTemplateRenderer>();
        services.TryAddSingleton<ITemplatedEmailService, TemplatedEmailService>();
        return services;
    }
}
