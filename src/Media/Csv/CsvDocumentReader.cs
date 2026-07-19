using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;

namespace WoW.Two.Sdk.Backend.Beta.Media.Csv;

/// <summary>Default <see cref="ICsvReader"/> over CsvHelper (invariant culture, header-mapped). Stateless and thread-safe.</summary>
public sealed class CsvDocumentReader : ICsvReader
{
    /// <inheritdoc />
    public async IAsyncEnumerable<T> ReadAsync<T>(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new StreamReader(source, leaveOpen: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture, leaveOpen: true);

        await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken))
            yield return record;
    }
}
