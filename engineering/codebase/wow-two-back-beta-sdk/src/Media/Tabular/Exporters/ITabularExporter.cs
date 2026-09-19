namespace WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;

/// <summary>
/// Defines export of supplied rows to a format-specific tabular document.
/// </summary>
/// <remarks>
/// The selected implementation owns schema, headers, ordering, culture and empty-input behavior.
/// CSV and XLSX do not promise identical columns for the same row type.
/// Resolve the concrete exporter or select from the registered set by Format.
/// </remarks>
public interface ITabularExporter
{
    /// <summary>Gets the format this exporter produces.</summary>
    TabularFormat Format { get; }

    /// <summary>Writes <paramref name="rows"/> to <paramref name="destination"/> in this exporter's format.</summary>
    /// <typeparam name="T">The row shape interpreted by the selected format's mapping rules.</typeparam>
    /// <param name="rows">The rows to export.</param>
    /// <param name="destination">The stream to write to (left open for the caller to dispose).</param>
    /// <param name="cancellationToken">Token to cancel the export.</param>
    /// <returns>A task that completes when the document has been written.</returns>
    /// <remarks>
    /// The caller owns the writable destination, which remains open on success or failure.
    /// Seeking, buffering and cancellation boundaries are implementation-specific.
    /// Writes are not transactional; failures or cancellation may leave partial output.
    /// Enumeration, mapping and destination errors propagate without rolling back output.
    /// </remarks>
    Task WriteAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default);
}
