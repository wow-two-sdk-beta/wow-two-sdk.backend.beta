namespace WoW.Two.Sdk.Backend.Beta.Media.Tabular;

/// <summary>
/// Exports a collection of rows to a tabular document (CSV or XLSX), using each row's public properties as
/// columns with a header row. One implementation per <see cref="TabularFormat"/>; inject the concrete
/// exporter, or resolve <c>IEnumerable&lt;ITabularExporter&gt;</c> and pick by <see cref="Format"/>.
/// </summary>
public interface ITabularExporter
{
    /// <summary>Gets the format this exporter produces.</summary>
    TabularFormat Format { get; }

    /// <summary>Writes <paramref name="rows"/> to <paramref name="destination"/> in this exporter's format.</summary>
    /// <typeparam name="T">The row type; its public properties become columns.</typeparam>
    /// <param name="rows">The rows to export.</param>
    /// <param name="destination">The stream to write to (left open for the caller to dispose).</param>
    /// <param name="cancellationToken">Token to cancel the export.</param>
    Task WriteAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default);
}
