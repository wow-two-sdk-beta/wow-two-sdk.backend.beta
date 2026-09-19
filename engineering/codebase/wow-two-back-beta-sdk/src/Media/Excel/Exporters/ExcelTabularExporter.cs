using ClosedXML.Excel;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel.Exporters;

/// <summary>Exports supplied rows as an XLSX document with one worksheet and a header-based table.</summary>
/// <remarks>
/// ClosedXML maps public fields and properties, including static members; indexers are ignored.
/// XLColumn Header, Order and Ignore attributes control schema; unannotated member order is not guaranteed.
/// Typed empty object sequences retain headers. Scalars use one column with a provider-generated header.
/// Values are stored as typed spreadsheet cells; display formatting belongs to the workbook and its reader.
/// </remarks>
public sealed class ExcelTabularExporter : ITabularExporter
{
    private const string WorksheetName = "Sheet1";

    /// <inheritdoc />
    /// <remarks>
    /// Builds the complete workbook in memory and saves synchronously; this method does not offload work.
    /// Cancellation is checked at entry only; enumeration, workbook creation and saving cannot be interrupted.
    /// A readable, seekable destination is rewound and truncated; other destinations must be fresh for a standalone XLSX.
    /// The stream stays open. Enumeration failure occurs before saving; a save failure can leave partial output.
    /// No rollback or atomic-write guarantee exists. Large inputs require memory for the complete workbook.
    /// </remarks>
    /// <exception cref="OperationCanceledException">Cancellation was requested when the method started.</exception>
    public TabularFormat Format => TabularFormat.Xlsx;

    /// <inheritdoc />
    public Task WriteAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);
        worksheet.Cell(1, 1).InsertTable(rows);
        workbook.SaveAs(destination);

        return Task.CompletedTask;
    }
}
