using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Maps a PDFsharp form to <see cref="PdfFormField"/>s, and fill values onto its fields.</summary>
internal static class PdfFormMapper
{
    private const int ReadOnlyFlag = 1;

    /// <summary>Whether the catalog holds a form; PDFsharp throws when asked for a form a document lacks.</summary>
    public static bool HasForm(PdfDocument document) => document.Internals.Catalog.Elements.ContainsKey("/AcroForm") && document.AcroForm is not null;

    public static IReadOnlyList<PdfFormField> Read(PdfAcroField.PdfAcroFieldCollection fields)
        => [.. Terminals(fields).Select(field => new PdfFormField
        {
            Name = field.Name,
            Type = TypeOf(field),
            Value = ValueOf(field),
            Options = OptionsOf(field),
            ReadOnly = (field.Elements.GetInteger("/Ff") & ReadOnlyFlag) != 0,
        })];

    public static void Fill(PdfAcroForm form, PdfFormFillSpec spec)
    {
        var fields = Terminals(form.Fields).ToDictionary(field => field.Name, StringComparer.Ordinal);
        var unknown = spec.Values.Keys.Where(name => !fields.ContainsKey(name)).ToList();
        if (unknown.Count > 0 && !spec.IgnoreUnknownFields)
            throw new PdfRejectedException("pdf_form_field_unknown", $"The form has no field named {string.Join(", ", unknown.Select(name => $"'{name}'"))}.");

        form.Elements.SetBoolean("/NeedAppearances", true);
        foreach (var (name, value) in spec.Values)
        {
            if (fields.TryGetValue(name, out var field))
                Apply(field, value ?? string.Empty);
        }

        if (!spec.LockFields)
            return;

        foreach (var field in fields.Values)
            field.Elements.SetInteger("/Ff", field.Elements.GetInteger("/Ff") | ReadOnlyFlag);
    }

    /// <summary>The fields that hold values: leaves, and radio groups whose kids are only widgets.</summary>
    private static IEnumerable<PdfAcroField> Terminals(PdfAcroField.PdfAcroFieldCollection fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            if (field.HasKids && field is not PdfRadioButtonField && field.Fields.Count > 0 && field.Fields[0] is not null && field.Fields[0].Name != field.Name)
            {
                foreach (var kid in Terminals(field.Fields))
                    yield return kid;
                continue;
            }

            yield return field;
        }
    }

    private static void Apply(PdfAcroField field, string value)
    {
        switch (field)
        {
            case PdfCheckBoxField box:
                box.Checked = value.Trim().ToUpperInvariant() is "TRUE" or "YES" or "ON" or "1" or "CHECKED"
                    || string.Equals(value.Trim(), OnState(box), StringComparison.Ordinal);
                break;
            case PdfRadioButtonField radio:
                var states = OptionsOf(radio);
                var chosen = FindIndex(states, value);
                if (chosen < 0)
                    throw Invalid(field, value, states);
                radio.SelectedIndex = chosen;
                break;
            case PdfChoiceField choice:
                var options = OptionsOf(choice);
                var editable = choice is PdfComboBoxField && (choice.Elements.GetInteger("/Ff") & (1 << 18)) != 0;
                if (options.Count > 0 && !editable && FindIndex(options, value) < 0)
                    throw Invalid(field, value, options);
                choice.Value = Text(value);
                break;
            case PdfSignatureField or PdfPushButtonField:
                throw new PdfRejectedException("pdf_form_value_invalid", $"The field '{field.Name}' cannot hold a value.");
            default:
                field.Value = Text(value);
                break;
        }
    }

    private static PdfString Text(string value)
        => value.All(character => character < 128) ? new PdfString(value) : new PdfString(value, PdfStringEncoding.Unicode);

    private static PdfFormFieldType TypeOf(PdfAcroField field) => field switch
    {
        PdfTextField => PdfFormFieldType.Text,
        PdfCheckBoxField => PdfFormFieldType.CheckBox,
        PdfRadioButtonField => PdfFormFieldType.RadioButton,
        PdfComboBoxField => PdfFormFieldType.ComboBox,
        PdfListBoxField => PdfFormFieldType.ListBox,
        PdfSignatureField => PdfFormFieldType.Signature,
        PdfPushButtonField => PdfFormFieldType.PushButton,
        _ => PdfFormFieldType.Other,
    };

    private static string ValueOf(PdfAcroField field) => field.Elements.GetValue("/V") switch
    {
        PdfString text => text.Value,
        PdfName name => name.Value.TrimStart('/'),
        PdfArray list => string.Join(", ", list.Elements.OfType<PdfString>().Select(item => item.Value)),
        _ => string.Empty,
    };

    /// <summary>A choice field's option texts, a radio group's states, or a check box's on-state.</summary>
    private static IReadOnlyList<string> OptionsOf(PdfAcroField field) => field switch
    {
        PdfChoiceField choice => choice.Elements.GetArray("/Opt") is { } options
            ? [.. options.Elements.Select(option => option switch
            {
                PdfString text => text.Value,
                PdfArray pair when pair.Elements.Count > 0 => (pair.Elements[0] as PdfString)?.Value ?? string.Empty,
                _ => string.Empty,
            })]
            : [],
        PdfCheckBoxField box => OnState(box) is { } on ? [on] : [],
        PdfRadioButtonField radio => [.. radio.GetAppearanceNames().Select(name => name.TrimStart('/')).Where(name => name != "Off")],
        _ => [],
    };

    private static string? OnState(PdfCheckBoxField box) => box.GetAppearanceNames().Select(name => name.TrimStart('/')).FirstOrDefault(name => name != "Off");

    private static int FindIndex(IReadOnlyList<string> options, string value)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index], value, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    private static PdfRejectedException Invalid(PdfAcroField field, string value, IReadOnlyList<string> options)
        => new("pdf_form_value_invalid", $"'{value}' is not an option of '{field.Name}' ({string.Join(", ", options)}).");
}
