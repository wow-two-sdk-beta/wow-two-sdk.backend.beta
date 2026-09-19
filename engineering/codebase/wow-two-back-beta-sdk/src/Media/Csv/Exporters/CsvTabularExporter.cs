using System.Globalization;
using System.Runtime.ExceptionServices;
using CsvHelper;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;

namespace WoW.Two.Sdk.Backend.Beta.Media.Csv.Exporters;

/// <summary>Exports supplied rows as a CSV document using invariant culture.</summary>
/// <remarks>
/// CsvHelper auto-maps public properties; member Name, Index and Ignore attributes control schema.
/// Unannotated order follows library mapping; no stable cross-version column order is promised.
/// Class-level delimiter/culture attributes are not applied by this writer's fixed configuration.
/// Typed object sequences include a header even when empty; scalar sequences have no header.
/// </remarks>
public sealed class CsvTabularExporter : ITabularExporter
{
    /// <inheritdoc />
    public TabularFormat Format => TabularFormat.Csv;

    /// <inheritdoc />
    /// <remarks>
    /// Writes comma-delimited UTF-8 without a BOM, using CRLF records and invariant default conversion.
    /// Rows are written incrementally through writer buffers; the complete document is not materialized.
    /// Writes start at the destination's current position without seeking or truncating; the stream stays open.
    /// Pre-cancellation writes nothing; cancellation during enumeration can leave buffered partial output.
    /// Enumeration and write failures also leave partial output; no rollback or atomic-write guarantee exists.
    /// </remarks>
    /// <exception cref="OperationCanceledException">The caller's cancellation is observed.</exception>
    public async Task WriteAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        await using var writer = new StreamWriter(destination, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture, leaveOpen: true);

        try
        {
            await csv.WriteRecordsAsync(rows, cancellationToken);
        }
        catch (WriterException exception) when (
            exception.InnerException is OperationCanceledException cancellation && cancellationToken.IsCancellationRequested)
        {
            ExceptionDispatchInfo.Capture(cancellation).Throw();
        }
    }
}
