namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Refers to the kind of an interactive form field.</summary>
public enum PdfFormFieldType
{
    /// <summary>A text box.</summary>
    Text,

    /// <summary>A check box; its value is its on-state name or <c>Off</c>.</summary>
    CheckBox,

    /// <summary>A group of radio buttons; its value is the chosen option.</summary>
    RadioButton,

    /// <summary>A drop-down list, optionally editable.</summary>
    ComboBox,

    /// <summary>A scrolling list.</summary>
    ListBox,

    /// <summary>A signature slot; filling it is not supported.</summary>
    Signature,

    /// <summary>A push button; it holds no value.</summary>
    PushButton,

    /// <summary>A field of another kind.</summary>
    Other,
}
