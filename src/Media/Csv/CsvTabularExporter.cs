using System.Globalization;
using CsvHelper;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;

namespace WoW.Two.Sdk.Backend.Beta.Media.Csv;

/// <summary>CSV <see cref="ITabularExporter"/> over CsvHelper (invariant culture). Stateless and thread-safe.</summary>
public sealed class CsvTabularExporter : ITabularExporter
{
    /// <inheritdoc />
    public TabularFormat Format => TabularFormat.Csv;

    /// <inheritdoc />
    public async Task WriteAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(destination);

        await using var writer = new StreamWriter(destination, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true);

        await csv.WriteRecordsAsync(rows, cancellationToken);
    }
}
