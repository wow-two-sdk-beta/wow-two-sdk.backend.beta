using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents a run of 1-based pages, <see cref="To"/> inclusive; an open end runs to the last page.</summary>
public sealed record PdfPageRange
{
    /// <summary>Gets the first page, from 1.</summary>
    public required int From { get; init; }

    /// <summary>Gets the last page, inclusive; null runs to the last page.</summary>
    public int? To { get; init; }

    /// <summary>A single page.</summary>
    /// <param name="page">The page, from 1.</param>
    public static PdfPageRange Single(int page) => new() { From = page, To = page };

    /// <summary>Parses a list such as <c>1-3, 5, 8-</c>; a descending run such as <c>5-3</c> reads backwards.</summary>
    /// <param name="text">The list.</param>
    /// <exception cref="PdfRejectedException">The list is empty or malformed.</exception>
    public static IReadOnlyList<PdfPageRange> Parse(string text)
    {
        var ranges = new List<PdfPageRange>();
        foreach (var part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', 2, StringSplitOptions.TrimEntries);
            if (!int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out var from) || from < 1)
                throw Invalid(text);

            if (bounds.Length == 1)
            {
                ranges.Add(Single(from));
                continue;
            }

            if (bounds[1].Length == 0)
            {
                ranges.Add(new PdfPageRange { From = from });
                continue;
            }

            if (!int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out var to) || to < 1)
                throw Invalid(text);
            ranges.Add(new PdfPageRange { From = from, To = to });
        }

        return ranges.Count > 0 ? ranges : throw Invalid(text);
    }

    /// <summary>The 1-based pages this range covers in a document of <paramref name="pageCount"/> pages.</summary>
    /// <param name="pageCount">The pages in the document.</param>
    /// <exception cref="PdfRejectedException">The range reaches past the last page.</exception>
    public IEnumerable<int> Pages(int pageCount)
    {
        var to = To ?? pageCount;
        if (From > pageCount || to > pageCount)
            throw new PdfRejectedException("pdf_page_range_invalid", $"Pages {From}-{to} reach past the document's {pageCount} pages.");

        return From <= to ? Enumerable.Range(From, to - From + 1) : Enumerable.Range(to, From - to + 1).Reverse();
    }

    private static PdfRejectedException Invalid(string? text)
        => new("pdf_page_range_invalid", $"'{text}' is not a page list such as 1-3,5,8-.");
}
