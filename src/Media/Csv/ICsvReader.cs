namespace WoW.Two.Sdk.Backend.Beta.Media.Csv;

/// <summary>Reads a CSV stream into strongly-typed records, mapping header columns to properties.</summary>
public interface ICsvReader
{
    /// <summary>Streams the CSV in <paramref name="source"/> as <typeparamref name="T"/> records.</summary>
    /// <typeparam name="T">The record type; header columns map to its properties.</typeparam>
    /// <param name="source">The CSV stream to read (left open for the caller to dispose).</param>
    /// <param name="cancellationToken">Token to stop reading.</param>
    /// <returns>An async sequence of parsed records.</returns>
    IAsyncEnumerable<T> ReadAsync<T>(Stream source, CancellationToken cancellationToken = default);
}
