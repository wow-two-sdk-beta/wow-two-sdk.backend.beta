using System.Globalization;
using System.IO.Compression;
using System.Net;
using MaxMind.Db;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Services;

/// <summary>Downloads the monthly DB-IP Lite release and swaps it in only after it opens as a valid database.</summary>
/// <remarks>
///   - the current month is tried first; before it is published, the previous month fills the gap
///   - a failed download keeps the existing file; a partial file never replaces it
/// </remarks>
public sealed partial class DbIpLiteDownloadService
{
    private readonly IHttpClientFactory _clients;
    private readonly DbIpLiteOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DbIpLiteDownloadService> _logger;

    /// <summary>Creates the service over the configured directory and release source.</summary>
    /// <param name="clients">The factory that supplies the download client.</param>
    /// <param name="options">The directory, edition and download limits.</param>
    /// <param name="clock">The clock that selects the release month.</param>
    /// <param name="logger">The logger for refresh outcomes.</param>
    public DbIpLiteDownloadService(
        IHttpClientFactory clients,
        DbIpLiteOptions options,
        TimeProvider clock,
        ILogger<DbIpLiteDownloadService> logger)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _clients = clients;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Gets the database file this service maintains.</summary>
    public string DatabasePath => PathFor(_options);

    /// <summary>Downloads the newest release unless the local copy already holds the current month.</summary>
    /// <param name="cancellationToken">Cancels the download; the existing file is kept.</param>
    /// <returns><see langword="true"/> when a newer database replaced the local file.</returns>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        string marker = DatabasePath + ".release";
        string? installed = File.Exists(DatabasePath) && File.Exists(marker)
            ? await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false)
            : null;

        Directory.CreateDirectory(_options.DatabaseDirectory);
        foreach (DateTimeOffset release in (DateTimeOffset[])[now, now.AddMonths(-1)])
        {
            if (string.Equals(installed, Month(release), StringComparison.Ordinal))
            {
                return false;
            }

            var uri = new Uri(string.Format(CultureInfo.InvariantCulture, _options.DownloadUrlFormat, EditionToken(_options.Edition), release));
            switch (await TryDownloadAsync(uri, cancellationToken).ConfigureAwait(false))
            {
                case true:
                    await File.WriteAllTextAsync(marker, Month(release), cancellationToken).ConfigureAwait(false);
                    LogRefreshed(uri);
                    return true;
                case false:
                    return false;
                case null:
                    continue;
            }
        }

        LogNoRelease(_options.Edition);
        return false;
    }

    internal static string PathFor(DbIpLiteOptions options) =>
        Path.Combine(options.DatabaseDirectory, $"dbip-{EditionToken(options.Edition)}-lite.mmdb");

    private async Task<bool?> TryDownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        string temporary = DatabasePath + ".download";
        try
        {
            using HttpClient client = _clients.CreateClient(IpLocationConstants.DbIpLiteHttpClientName);
            using HttpResponseMessage response = await client
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var decompressed = new GZipStream(body, CompressionMode.Decompress);
                await using (decompressed.ConfigureAwait(false))
                {
                    var file = File.Create(temporary);
                    await using (file.ConfigureAwait(false))
                    {
                        await CopyBoundedAsync(decompressed, file, _options.MaxDatabaseBytes, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            using (new Reader(temporary, FileAccessMode.Memory))
            {
                // Opening validates the search tree and metadata before the swap.
            }

            File.Move(temporary, DatabasePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException
            or InvalidDatabaseException or UnauthorizedAccessException)
        {
            LogRefreshFailed(exception, uri);
            return false;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static async Task CopyBoundedAsync(Stream source, Stream target, long limit, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new InvalidDataException($"The database exceeds the {limit}-byte limit.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover partial file is overwritten by the next attempt.
        }
    }

    private static string Month(DateTimeOffset value) => value.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string EditionToken(DbIpLiteEdition edition) => edition switch
    {
        DbIpLiteEdition.City => "city",
        _ => "country",
    };

    [LoggerMessage(EventId = 7105, Level = LogLevel.Information, Message = "Refreshed the DB-IP Lite database from {Uri}")]
    private partial void LogRefreshed(Uri uri);

    [LoggerMessage(EventId = 7106, Level = LogLevel.Warning, Message = "DB-IP Lite refresh from {Uri} failed; the existing database stays active")]
    private partial void LogRefreshFailed(Exception exception, Uri uri);

    [LoggerMessage(EventId = 7107, Level = LogLevel.Warning, Message = "No DB-IP Lite {Edition} release is published for this or the previous month")]
    private partial void LogNoRelease(DbIpLiteEdition edition);
}
