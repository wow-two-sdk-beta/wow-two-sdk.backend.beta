namespace WoW.Two.Sdk.Backend.Beta.Media.Tabular.Parsers;

/// <summary>Defines reading a tabular document into header-mapped records, one implementation per format.</summary>
/// <remarks>Resolve the concrete parser or select from the registered set by <see cref="Format"/>, such as by an upload's extension.</remarks>
public interface ITabularParser
{
    /// <summary>Gets the format this parser reads.</summary>
    TabularFormat Format { get; }

    /// <summary>Streams the rows of <paramref name="source"/> as <typeparamref name="T"/> records.</summary>
    /// <typeparam name="T">The record type; header columns map to its properties.</typeparam>
    /// <param name="source">The document (left open for the caller to dispose).</param>
    /// <param name="cancellationToken">Token to stop reading.</param>
    IAsyncEnumerable<T> ReadAsync<T>(Stream source, CancellationToken cancellationToken = default);
}
