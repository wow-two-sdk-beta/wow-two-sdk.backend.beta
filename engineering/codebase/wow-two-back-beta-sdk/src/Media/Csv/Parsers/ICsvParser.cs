namespace WoW.Two.Sdk.Backend.Beta.Media.Csv.Parsers;

/// <summary>Defines CSV stream parsing into strongly typed, header-mapped records.</summary>
public interface ICsvParser
{
    /// <summary>Streams the CSV in <paramref name="source"/> as <typeparamref name="T"/> records.</summary>
    /// <typeparam name="T">The record type; header columns map to its properties.</typeparam>
    /// <param name="source">The CSV stream to read (left open for the caller to dispose).</param>
    /// <param name="cancellationToken">Token to stop reading.</param>
    /// <returns>An async sequence of parsed records.</returns>
    /// <remarks>
    /// Enumeration reads lazily from the current position without seeking; keep the readable stream open until it ends or is disposed.
    /// The parser leaves the stream open. UTF-8 is the default with byte-order-mark detection; empty input yields no records.
    /// CsvHelper header, malformed-data and conversion failures propagate during enumeration, possibly after earlier records were yielded.
    /// Yielded records remain available without rollback. Cancellation is forwarded to CsvHelper's asynchronous record enumeration.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The source is null, checked when enumeration starts.</exception>
    /// <exception cref="OperationCanceledException">Enumeration observes cancellation.</exception>
    IAsyncEnumerable<T> ReadAsync<T>(Stream source, CancellationToken cancellationToken = default);
}
