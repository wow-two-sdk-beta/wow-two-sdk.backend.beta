using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>
/// Maps a template to a filled document: repeats table rows per collection item, then replaces placeholders run by run
/// so Word's run splitting and each placeholder's formatting survive.
/// </summary>
internal static partial class WordTemplateMapper
{
    /// <summary>Fills every part holding text and returns the placeholders the data lacked, in first-seen order.</summary>
    public static IReadOnlyList<string> Fill(MainDocumentPart main, JsonElement data, WordMissingValue missing)
    {
        var misses = new List<string>();
        foreach (var root in Roots(main))
        {
            ExpandRows(root, data, missing, misses);
            foreach (var paragraph in root.Descendants<Paragraph>().ToList())
                FillParagraph(paragraph, path => Resolve(data, path), missing, misses);
        }

        return misses;
    }

    private static IEnumerable<OpenXmlPartRootElement> Roots(MainDocumentPart main)
    {
        if (main.Document is { } document)
            yield return document;

        foreach (var header in main.HeaderParts)
        {
            if (header.Header is { } root)
                yield return root;
        }

        foreach (var footer in main.FooterParts)
        {
            if (footer.Footer is { } root)
                yield return root;
        }

        if (main.FootnotesPart?.Footnotes is { } footnotes)
            yield return footnotes;

        if (main.EndnotesPart?.Endnotes is { } endnotes)
            yield return endnotes;
    }

    /// <summary>Repeats each row naming <c>{{Collection[]…}}</c> once per item, filling the item paths in each copy.</summary>
    private static void ExpandRows(OpenXmlPartRootElement root, JsonElement data, WordMissingValue missing, List<string> misses)
    {
        foreach (var row in root.Descendants<TableRow>().ToList())
        {
            var match = RowPlaceholder().Match(string.Concat(row.Descendants<Text>().Select(text => text.Text)));
            if (!match.Success)
                continue;

            var collection = match.Groups[1].Value.Trim();
            if (Find(data, collection) is not { ValueKind: JsonValueKind.Array } items)
            {
                Miss(misses, collection + "[]");
                if (missing == WordMissingValue.Empty)
                    row.Remove();

                continue;
            }

            OpenXmlElement anchor = row;
            foreach (var item in items.EnumerateArray())
            {
                var copy = (TableRow)row.CloneNode(true);
                foreach (var paragraph in copy.Descendants<Paragraph>().ToList())
                    FillParagraph(paragraph, path => ItemPath(collection, path) is { } relative ? Format(Find(item, relative)) : Resolve(data, path), missing, misses);

                anchor.InsertAfterSelf(copy);
                anchor = copy;
            }

            row.Remove();
        }
    }

    /// <summary>The path inside an item for <c>Collection[].Field</c>, empty for <c>Collection[]</c>; null for other paths.</summary>
    private static string? ItemPath(string collection, string path)
    {
        var prefix = collection + "[]";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return path[prefix.Length..].TrimStart('.');
    }

    /// <summary>Replaces the placeholders of one paragraph; a value's line breaks become Word breaks.</summary>
    private static void FillParagraph(Paragraph paragraph, Func<string, string?> resolve, WordMissingValue missing, List<string> misses)
    {
        var texts = paragraph.Descendants<Text>().Where(text => text.Parent is Run).ToList();
        if (texts.Count == 0 || !string.Concat(texts.Select(text => text.Text)).Contains("{{", StringComparison.Ordinal))
            return;

        var matches = Placeholder().Matches(string.Concat(texts.Select(text => text.Text)));
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            var match = matches[index];
            var path = match.Groups[1].Value.Trim();
            var value = resolve(path);
            if (value is null)
            {
                Miss(misses, path);
                if (missing != WordMissingValue.Empty)
                    continue;

                value = string.Empty;
            }

            Replace(texts, match.Index, match.Length, value);
        }

        foreach (var text in texts.Where(text => text.Text.Contains('\n', StringComparison.Ordinal)).ToList())
            SplitLines(text);
    }

    /// <summary>Replaces characters <paramref name="start"/>..+<paramref name="length"/> of the joined texts with <paramref name="value"/>.</summary>
    private static void Replace(List<Text> texts, int start, int length, string value)
    {
        var end = start + length;
        var offset = 0;
        var first = true;
        foreach (var text in texts)
        {
            var content = text.Text;
            var from = offset;
            var to = offset + content.Length;
            offset = to;
            if (to <= start || from >= end)
                continue;

            var cutFrom = Math.Max(start, from) - from;
            var cutTo = Math.Min(end, to) - from;
            text.Text = content[..cutFrom] + (first ? value : string.Empty) + content[cutTo..];
            text.Space = SpaceProcessingModeValues.Preserve;
            first = false;
        }
    }

    /// <summary>Turns a text holding line feeds into texts separated by breaks, inside the same run.</summary>
    private static void SplitLines(Text text)
    {
        var lines = text.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        text.Text = lines[0];
        OpenXmlElement anchor = text;
        foreach (var line in lines.Skip(1))
        {
            var lineBreak = new Break();
            anchor.InsertAfterSelf(lineBreak);
            var next = new Text(line) { Space = SpaceProcessingModeValues.Preserve };
            lineBreak.InsertAfterSelf(next);
            anchor = next;
        }
    }

    private static void Miss(List<string> misses, string path)
    {
        if (!misses.Contains(path, StringComparer.Ordinal))
            misses.Add(path);
    }

    private static string? Resolve(JsonElement data, string path) => Format(Find(data, path));

    /// <summary>The element at a dotted path; names match case-insensitively, numbers index arrays.</summary>
    private static JsonElement? Find(JsonElement data, string path)
    {
        var current = data;
        if (path.Length == 0)
            return current;

        foreach (var segment in path.Split('.', StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && Property(current, segment) is { } property)
                current = property;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < current.GetArrayLength())
                current = current[index];
            else
                return null;
        }

        return current;
    }

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var exact))
            return exact;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    /// <summary>The text of a value: strings as written, numbers and booleans as JSON, arrays joined; objects count as missing.</summary>
    private static string? Format(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.String => value.Value.GetString(),
        JsonValueKind.Number => value.Value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Array => string.Join(", ", value.Value.EnumerateArray().Select(item => Format(item) ?? string.Empty)),
        _ => null,
    };

    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{\{\s*([^{}\[\]]+?)\[\]")]
    private static partial Regex RowPlaceholder();
}
