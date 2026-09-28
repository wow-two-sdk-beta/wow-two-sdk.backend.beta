using WoW.Two.Sdk.Backend.Beta.Media.Tabular;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel.Parsers;

/// <summary>Defines XLSX parsing into header-mapped records.</summary>
public interface IExcelParser
{
    /// <summary>Streams the rows of a worksheet as <typeparamref name="T"/> records; the first used row holds the headers.</summary>
    /// <typeparam name="T">The record type; a header maps to the property its <c>XLColumn</c> header or its name matches.</typeparam>
    /// <param name="source">The workbook (left open for the caller to dispose).</param>
    /// <param name="worksheet">The worksheet name; null reads the first worksheet.</param>
    /// <param name="cancellationToken">Token to stop reading.</param>
    /// <exception cref="TabularRowException">A cell does not convert to its property's type.</exception>
    IAsyncEnumerable<T> ReadAsync<T>(Stream source, string? worksheet = null, CancellationToken cancellationToken = default);
}
