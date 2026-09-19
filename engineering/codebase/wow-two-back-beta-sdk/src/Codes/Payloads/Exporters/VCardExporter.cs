using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Extensions;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Exporters;

/// <summary>Exports supplied contact data as a vCard 3.0 document.</summary>
public sealed class VCardExporter : IVCardExporter
{
    /// <summary>Exports one card, omitting blank optional fields and folding UTF-8 content lines.</summary>
    public string Export(ContactCardModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var first = model.FirstName?.Trim();
        var last = model.LastName?.Trim();
        var lines = new List<string>
        {
            "BEGIN:VCARD", "VERSION:3.0",
            $"N:{ContentLineExtensions.Escape(last)};{ContentLineExtensions.Escape(first)};;;",
            "FN:" + ContentLineExtensions.Escape(string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrEmpty(s)))),
        };
        Add(lines, "ORG:", model.Organization);
        Add(lines, "TITLE:", model.Title);
        Add(lines, "TEL;TYPE=CELL:", model.Phone);
        Add(lines, "EMAIL:", model.Email);
        Add(lines, "URL:", model.Url);
        if (!string.IsNullOrWhiteSpace(model.Address)) lines.Add("ADR:;;" + ContentLineExtensions.Escape(model.Address) + ";;;;");
        Add(lines, "NOTE:", model.Note);
        lines.Add("END:VCARD");
        return ContentLineExtensions.Join(lines);
    }

    private static void Add(List<string> lines, string prefix, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add(prefix + ContentLineExtensions.Escape(value));
    }
}
