using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClosedXML.Attributes;
using ClosedXML.Excel;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Parsers;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel.Parsers;

/// <summary>
/// Parses XLSX worksheets into header-mapped records through ClosedXML. Headers match the <c>XLColumn</c> header the
/// exporter writes, else the property name ignoring case, spaces, underscores and dashes; unmatched columns are skipped,
/// blank rows too. Numbers, booleans, dates, enums and GUIDs convert from typed cells or invariant text.
/// </summary>
public sealed class ExcelDocumentParser : IExcelParser, ITabularParser
{
    /// <inheritdoc />
    public TabularFormat Format => TabularFormat.Xlsx;

    /// <inheritdoc />
    public IAsyncEnumerable<T> ReadAsync<T>(Stream source, CancellationToken cancellationToken = default)
        => ReadAsync<T>(source, null, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<T> ReadAsync<T>(Stream source, string? worksheet = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var workbook = new XLWorkbook(buffer);
        var sheet = worksheet is null ? workbook.Worksheets.First() : workbook.Worksheet(worksheet);
        if (sheet.RangeUsed() is not { } range)
            yield break;

        var columns = Columns<T>(range.FirstRow());
        foreach (var row in range.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = Activator.CreateInstance<T>();
            foreach (var (index, header, property) in columns)
            {
                var cell = row.Cell(index);
                if (cell.IsEmpty())
                    continue;

                property.SetValue(record, Convert(cell, property.PropertyType, row.RowNumber(), header));
            }

            yield return record;
        }
    }

    private static List<(int Index, string Header, PropertyInfo Property)> Columns<T>(IXLRangeRow headers)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite).ToList();
        var columns = new List<(int, string, PropertyInfo)>();
        foreach (var cell in headers.CellsUsed())
        {
            var header = cell.GetString().Trim();
            var property = properties.FirstOrDefault(candidate => string.Equals(candidate.GetCustomAttribute<XLColumnAttribute>()?.Header, header, StringComparison.OrdinalIgnoreCase))
                ?? properties.FirstOrDefault(candidate => Normalize(candidate.Name) == Normalize(header));
            if (property is not null)
                columns.Add((cell.Address.ColumnNumber - headers.FirstCell().Address.ColumnNumber + 1, header, property));
        }

        return columns;
    }

    private static string Normalize(string name)
        => new([.. name.Where(character => character is not (' ' or '_' or '-')).Select(char.ToUpperInvariant)]);

    private static object? Convert(IXLCell cell, Type type, int row, string header)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        var value = cell.Value;
        try
        {
            if (target == typeof(string))
                return value.IsNumber ? value.GetNumber().ToString(CultureInfo.InvariantCulture) : cell.GetFormattedString();
            if (value.IsError)
                throw new FormatException($"the cell holds the error {value.GetError()}");

            return target switch
            {
                _ when target == typeof(bool) => value.IsBoolean ? value.GetBoolean() : Truthy(value.ToString(CultureInfo.InvariantCulture)),
                _ when target.IsEnum => value.IsNumber
                    ? Enum.ToObject(target, (long)value.GetNumber())
                    : Enum.Parse(target, value.ToString(CultureInfo.InvariantCulture).Trim(), ignoreCase: true),
                _ when target == typeof(Guid) => Guid.Parse(value.ToString(CultureInfo.InvariantCulture).Trim()),
                _ when target == typeof(DateTime) => Moment(value),
                _ when target == typeof(DateTimeOffset) => new DateTimeOffset(DateTime.SpecifyKind(Moment(value), DateTimeKind.Utc)),
                _ when target == typeof(DateOnly) => DateOnly.FromDateTime(Moment(value)),
                _ when target == typeof(TimeOnly) => value.IsTimeSpan ? TimeOnly.FromTimeSpan(value.GetTimeSpan()) : TimeOnly.FromDateTime(Moment(value)),
                _ when target == typeof(TimeSpan) => value.IsTimeSpan ? value.GetTimeSpan() : TimeSpan.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                _ => value.IsNumber
                    ? System.Convert.ChangeType(value.GetNumber(), target, CultureInfo.InvariantCulture)
                    : System.Convert.ChangeType(value.ToString(CultureInfo.InvariantCulture).Trim(), target, CultureInfo.InvariantCulture),
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new TabularRowException(row, header, $"'{cell.GetFormattedString()}' is not a {target.Name} ({exception.Message}).");
        }
    }

    private static DateTime Moment(XLCellValue value) => value switch
    {
        { IsDateTime: true } => value.GetDateTime(),
        { IsNumber: true } => DateTime.FromOADate(value.GetNumber()),
        _ => DateTime.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
    };

    private static bool Truthy(string text) => text.Trim().ToUpperInvariant() switch
    {
        "TRUE" or "YES" or "Y" or "1" or "ON" => true,
        "FALSE" or "NO" or "N" or "0" or "OFF" or "" => false,
        _ => throw new FormatException("expected yes/no, true/false or 1/0"),
    };
}
