namespace WoW.Two.Sdk.Backend.Beta.Media.Tabular;

/// <summary>The tabular document formats the SDK can export a row collection to.</summary>
public enum TabularFormat
{
    /// <summary>Comma-separated values (<c>.csv</c>), via CsvHelper.</summary>
    Csv,

    /// <summary>Excel workbook (<c>.xlsx</c>), via ClosedXML.</summary>
    Xlsx,
}
