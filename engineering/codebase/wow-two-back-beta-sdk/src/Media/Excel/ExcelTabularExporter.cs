using ClosedXML.Excel;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel;

/// <summary>Excel (<c>.xlsx</c>) <see cref="ITabularExporter"/> over ClosedXML — one worksheet, header row + data as a table. Stateless and thread-safe.</summary>
public sealed class ExcelTabularExporter : ITabularExporter
{
    private const string WorksheetName = "Sheet1";

    /// <inheritdoc />
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
