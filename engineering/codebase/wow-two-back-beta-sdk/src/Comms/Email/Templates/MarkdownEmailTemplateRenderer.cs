using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Localization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Email.Templates;

/// <summary>
/// Renders Markdown templates into a branded, single-column HTML email with inlined styles, and a plain-text twin from
/// the same parse. Raw HTML in a template is shown as text; values are Markdown-escaped before they are placed.
/// </summary>
/// <param name="options">Templates and brand; <c>Comms:Email:Templates</c> reloads live.</param>
public sealed partial class MarkdownEmailTemplateRenderer(IOptionsMonitor<EmailTemplateOptions> options) : IEmailTemplateRenderer
{
    private const string Font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseEmphasisExtras().UseAutoLinks().DisableHtml().Build();

    /// <inheritdoc />
    public EmailTemplateResult Render(string template, IReadOnlyDictionary<string, object?> values, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(values);
        var current = options.CurrentValue;
        var (content, cultureName) = Find(current, template, culture);
        var formatCulture = CultureInfo.GetCultureInfo(cultureName);

        var subject = Fill(content.Subject, values, formatCulture, template).ReplaceLineEndings(" ").Trim();
        var escaped = values.ToDictionary(pair => pair.Key, pair => pair.Value is string text ? (object?)EscapeMarkdown(text) : pair.Value, StringComparer.Ordinal);
        var markdown = Fill(content.Body, escaped, formatCulture, template);
        var preheader = content.Preheader is null ? null : Fill(content.Preheader, values, formatCulture, template);
        var document = Markdig.Markdown.Parse(markdown, Pipeline);

        return new EmailTemplateResult
        {
            Subject = subject,
            Html = Layout(current.Brand, subject, preheader, Style(document.ToHtml(Pipeline), current.Brand.AccentColor), cultureName),
            Text = Text(document, current.Brand),
            Culture = cultureName,
        };
    }

    /// <summary>The template in the culture, its parents, or the default culture.</summary>
    private static (EmailTemplateContentOptions Content, string Culture) Find(EmailTemplateOptions options, string template, CultureInfo? culture)
    {
        if (!options.Templates.TryGetValue(template, out var cultures))
            throw new EmailTemplateRejectedException("email_template_missing", $"No email template '{template}' is configured.");

        for (var candidate = culture; candidate is not null && !Equals(candidate, CultureInfo.InvariantCulture); candidate = candidate.Parent)
        {
            if (cultures.TryGetValue(candidate.Name, out var found))
                return (found, candidate.Name);
        }

        return cultures.TryGetValue(options.DefaultCulture, out var fallback)
            ? (fallback, options.DefaultCulture)
            : throw new EmailTemplateRejectedException("email_template_missing", $"Email template '{template}' has no '{options.DefaultCulture}' version.");
    }

    private static string Fill(string text, IReadOnlyDictionary<string, object?> values, CultureInfo culture, string template)
    {
        if (MessageTemplateMapper.TryFormat(text, values, culture, out var filled))
            return filled;

        var missing = Placeholder().Matches(text).Select(match => match.Groups[1].Value).Where(name => !values.ContainsKey(name)).Distinct(StringComparer.Ordinal);
        throw new EmailTemplateRejectedException("email_template_value_missing", $"Email template '{template}' needs values for: {string.Join(", ", missing)}.");
    }

    private static string EscapeMarkdown(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if ("\\`*_{}[]()#+-.!<>|~".Contains(character, StringComparison.Ordinal))
                escaped.Append('\\');

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    /// <summary>
    /// Inlines styles on the tags Markdig writes, and turns lone links titled "button" into buttons. The button's anchor
    /// leads with its style, so the link rule below never gives it a second one.
    /// </summary>
    private static string Style(string html, string accent)
    {
        var color = SafeColor(accent);
        html = Button().Replace(html, match =>
            $"""<table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 24px 0;"><tr><td style="border-radius:6px;background:{color};"><a style="display:inline-block;padding:12px 24px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;" href="{match.Groups[1].Value}">{match.Groups[2].Value}</a></td></tr></table>""");
        return html
            .Replace("<p>", "<p style=\"margin:0 0 16px 0;\">", StringComparison.Ordinal)
            .Replace("<h1>", "<h1 style=\"margin:0 0 16px 0;font-size:24px;line-height:1.3;\">", StringComparison.Ordinal)
            .Replace("<h2>", "<h2 style=\"margin:24px 0 12px 0;font-size:20px;line-height:1.3;\">", StringComparison.Ordinal)
            .Replace("<h3>", "<h3 style=\"margin:20px 0 8px 0;font-size:17px;line-height:1.3;\">", StringComparison.Ordinal)
            .Replace("<a href=", $"<a style=\"color:{color};\" href=", StringComparison.Ordinal)
            .Replace("<ul>", "<ul style=\"margin:0 0 16px 0;padding-left:24px;\">", StringComparison.Ordinal)
            .Replace("<ol>", "<ol style=\"margin:0 0 16px 0;padding-left:24px;\">", StringComparison.Ordinal)
            .Replace("<blockquote>", "<blockquote style=\"margin:0 0 16px 0;padding-left:12px;border-left:3px solid #e4e4e7;color:#52525b;\">", StringComparison.Ordinal)
            .Replace("<pre>", "<pre style=\"margin:0 0 16px 0;padding:12px;background:#f4f4f5;border-radius:6px;overflow:auto;\">", StringComparison.Ordinal)
            .Replace("<code>", "<code style=\"font-family:Consolas,Menlo,monospace;font-size:14px;background:#f4f4f5;padding:1px 4px;border-radius:4px;\">", StringComparison.Ordinal)
            .Replace("<hr />", "<hr style=\"border:none;border-top:1px solid #e4e4e7;margin:24px 0;\" />", StringComparison.Ordinal)
            .Replace("<table>", "<table style=\"border-collapse:collapse;margin:0 0 16px 0;\">", StringComparison.Ordinal)
            .Replace("<th>", "<th style=\"border:1px solid #e4e4e7;padding:6px 10px;text-align:left;\">", StringComparison.Ordinal)
            .Replace("<td>", "<td style=\"border:1px solid #e4e4e7;padding:6px 10px;\">", StringComparison.Ordinal);
    }

    private static string Layout(EmailBrandOptions brand, string subject, string? preheader, string content, string culture)
    {
        var name = WebUtility.HtmlEncode(brand.Name);
        var header = brand.LogoUrl is { IsAbsoluteUri: true } logo
            ? $"""<img src="{WebUtility.HtmlEncode(logo.ToString())}" alt="{name}" height="32" style="display:block;height:32px;border:0;" />"""
            : $"""<span style="font-size:18px;font-weight:700;color:#18181b;">{name}</span>""";
        var footer = WebUtility.HtmlEncode(brand.FooterText ?? brand.Name);
        if (brand.WebsiteUrl is { IsAbsoluteUri: true } website)
            footer += $"""<br /><a href="{WebUtility.HtmlEncode(website.ToString())}" style="color:#71717a;">{WebUtility.HtmlEncode(website.Host)}</a>""";

        var hidden = preheader is null ? string.Empty : $"""<span style="display:none;visibility:hidden;opacity:0;color:transparent;height:0;width:0;overflow:hidden;">{WebUtility.HtmlEncode(preheader)}</span>""";
        return $"""
            <!DOCTYPE html>
            <html lang="{WebUtility.HtmlEncode(culture)}">
            <head><meta charset="utf-8" /><meta name="viewport" content="width=device-width,initial-scale=1" /><meta name="color-scheme" content="light" /><title>{WebUtility.HtmlEncode(subject)}</title></head>
            <body style="margin:0;padding:0;background:#f4f4f5;">
            {hidden}
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;"><tr><td align="center" style="padding:24px 12px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:8px;">
            <tr><td style="padding:24px 32px 0 32px;font-family:{Font};">{header}</td></tr>
            <tr><td style="padding:16px 32px 24px 32px;font-family:{Font};font-size:16px;line-height:1.6;color:#18181b;">{content}</td></tr>
            </table>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;"><tr><td style="padding:16px 32px;font-family:{Font};font-size:12px;line-height:1.5;color:#71717a;text-align:center;">{footer}</td></tr></table>
            </td></tr></table>
            </body>
            </html>
            """;
    }

    /// <summary>The plain-text twin: paragraphs, headings, lists, quotes, code and tables, with each link's URL written out.</summary>
    private static string Text(MarkdownDocument document, EmailBrandOptions brand)
    {
        var blocks = new List<string>();
        foreach (var block in document)
            Block(block, blocks, prefix: string.Empty);

        blocks.Add("—\n" + (brand.FooterText ?? brand.Name) + (brand.WebsiteUrl is { } website ? "\n" + website : string.Empty));
        return string.Join("\n\n", blocks.Where(text => text.Length > 0));
    }

    private static void Block(Block block, List<string> blocks, string prefix)
    {
        switch (block)
        {
            case HeadingBlock heading:
                blocks.Add(prefix + Inline(heading.Inline));
                break;
            case ParagraphBlock paragraph:
                blocks.Add(prefix + Inline(paragraph.Inline));
                break;
            case ListBlock list:
                var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                blocks.Add(string.Join('\n', list.OfType<ListItemBlock>().Select(item =>
                {
                    var marker = list.IsOrdered ? $"{(number++).ToString(CultureInfo.InvariantCulture)}. " : "- ";
                    var parts = new List<string>();
                    foreach (var child in item)
                        Block(child, parts, string.Empty);

                    return prefix + marker + string.Join("\n  ", parts);
                })));
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                    Block(child, blocks, prefix + "> ");
                break;
            case CodeBlock code:
                blocks.Add(string.Join('\n', code.Lines.Lines.Take(code.Lines.Count).Select(line => prefix + line.Slice.ToString())));
                break;
            case ThematicBreakBlock:
                blocks.Add(prefix + "---");
                break;
            case Table table:
                blocks.Add(string.Join('\n', table.OfType<TableRow>().Select(row => prefix + string.Join(" | ", row.OfType<TableCell>().Select(cell => string.Join(' ', cell.OfType<ParagraphBlock>().Select(p => Inline(p.Inline))))))));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    Block(child, blocks, prefix);
                break;
        }
    }

    private static string Inline(ContainerInline? container)
    {
        var text = new StringBuilder();
        for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LinkInline { IsImage: true }:
                    break;
                case LinkInline link:
                    var label = Inline(link);
                    text.Append(string.Equals(label, link.Url, StringComparison.Ordinal) || link.Url is null ? label : $"{label} ({link.Url})");
                    break;
                case AutolinkInline auto:
                    text.Append(auto.Url);
                    break;
                case LineBreakInline:
                    text.Append('\n');
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.ToString());
                    break;
                case HtmlInline html:
                    text.Append(html.Tag);
                    break;
                case ContainerInline nested:
                    text.Append(Inline(nested));
                    break;
            }
        }

        return text.ToString();
    }

    private static string SafeColor(string color) => HexColor().IsMatch(color) ? color : "#4F46E5";

    [GeneratedRegex(@"\{\s*([A-Za-z_][\w.]*)")]
    private static partial Regex Placeholder();

    [GeneratedRegex("""<p><a href="([^"]+)" title="button">(.*?)</a></p>""")]
    private static partial Regex Button();

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}){1,2}$")]
    private static partial Regex HexColor();
}
