using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>Markdown registration.</summary>
public static class MarkdownServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMarkdownService"/> over Markdig. Options come from <paramref name="configure"/>, then the
    /// host section <c>Media:Markdown</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Raw HTML, link treatment and reading speed.</param>
    public static IServiceCollection AddMarkdown(this IServiceCollection services, Action<MarkdownOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            MarkdownOptions.SectionName,
            configure,
            builder => builder.Validate(o => o.WordsPerMinute > 0, "MarkdownOptions.WordsPerMinute must be positive."));
        services.TryAddSingleton<IMarkdownService, MarkdigMarkdownService>();
        return services;
    }
}
