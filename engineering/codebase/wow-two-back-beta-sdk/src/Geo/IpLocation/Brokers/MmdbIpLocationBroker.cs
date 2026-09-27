using System.Collections;
using System.Net;
using System.Net.Sockets;
using MaxMind.Db;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Models;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Brokers;

/// <summary>Integrates MaxMind DB files — free DB-IP Lite or GeoLite2, or paid GeoIP2 — for IP location.</summary>
/// <remarks>
///   - loads the whole file into memory and reloads it when its write time changes
///   - a missing or unreadable file resolves nothing and keeps the last good database
///   - lookups never throw for data faults; they return <see langword="null"/>
/// </remarks>
public sealed partial class MmdbIpLocationBroker : IIpLocationBroker, IDisposable
{
    private readonly MmdbIpLocationOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<MmdbIpLocationBroker> _logger;
    private readonly OutboundAddressValidator _addresses = new();
    private readonly Lock _gate = new();
    private Reader? _reader;
    private DateTime _loadedWriteTimeUtc;
    private long _nextCheckTicks;
    private bool _reportedMissing;

    /// <summary>Creates the broker over the configured database file.</summary>
    /// <param name="options">The database path and reload interval.</param>
    /// <param name="clock">The clock that paces write-time checks.</param>
    /// <param name="logger">The logger for load and lookup failures.</param>
    public MmdbIpLocationBroker(MmdbIpLocationOptions options, TimeProvider clock, ILogger<MmdbIpLocationBroker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public IpLocationModel? Locate(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        IPAddress ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (!_addresses.IsAllowed(ip) || CurrentReader() is not { } reader)
        {
            return null;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && reader.Metadata.IPVersion == 4)
        {
            return null;
        }

        try
        {
            return reader.Find<Dictionary<string, object>>(ip) is { } record ? ToModel(record) : null;
        }
        catch (InvalidDatabaseException exception)
        {
            LogLookupFailed(exception);
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _reader?.Dispose();
            _reader = null;
        }
    }

    private Reader? CurrentReader()
    {
        long now = _clock.GetUtcNow().UtcTicks;
        if (now < Volatile.Read(ref _nextCheckTicks))
        {
            return Volatile.Read(ref _reader);
        }

        lock (_gate)
        {
            if (now >= _nextCheckTicks)
            {
                Reload();
                Volatile.Write(ref _nextCheckTicks, now + _options.ReloadCheckInterval.Ticks);
            }

            return _reader;
        }
    }

    private void Reload()
    {
        string path = _options.DatabasePath;
        if (!File.Exists(path))
        {
            if (!_reportedMissing)
            {
                _reportedMissing = true;
                LogDatabaseMissing(path);
            }

            return;
        }

        _reportedMissing = false;
        DateTime writeTime = File.GetLastWriteTimeUtc(path);
        if (_reader is not null && writeTime == _loadedWriteTimeUtc)
        {
            return;
        }

        try
        {
            // The replaced reader holds only managed memory, so lookups still using it finish safely.
            var next = new Reader(path, FileAccessMode.Memory);
            Volatile.Write(ref _reader, next);
            _loadedWriteTimeUtc = writeTime;
            LogDatabaseLoaded(path, next.Metadata.DatabaseType, next.Metadata.BuildDate);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDatabaseException)
        {
            LogDatabaseLoadFailed(exception, path);
        }
    }

    private static IpLocationModel? ToModel(Dictionary<string, object> record)
    {
        IDictionary<string, object>? country = Map(record, "country") ?? Map(record, "registered_country");
        if (Text(country, "iso_code") is not { Length: 2 } code)
        {
            return null;
        }

        IDictionary<string, object>? location = Map(record, "location");
        return new IpLocationModel
        {
            CountryCode = code.ToUpperInvariant(),
            CountryName = EnglishName(country),
            ContinentCode = Text(Map(record, "continent"), "code"),
            Subdivision = EnglishName(First(record, "subdivisions")),
            City = EnglishName(Map(record, "city")),
            Coordinate = Number(location, "latitude") is { } latitude and >= -90 and <= 90
                && Number(location, "longitude") is { } longitude and >= -180 and <= 180
                ? new GeoCoordinate(latitude, longitude)
                : null,
        };
    }

    private static IDictionary<string, object>? Map(IDictionary<string, object>? parent, string key) =>
        parent is not null && parent.TryGetValue(key, out object? value) ? value as IDictionary<string, object> : null;

    private static IDictionary<string, object>? First(Dictionary<string, object> parent, string key) =>
        parent.TryGetValue(key, out object? value) && value is IList { Count: > 0 } items
            ? items[0] as IDictionary<string, object>
            : null;

    private static string? Text(IDictionary<string, object>? parent, string key) =>
        parent is not null && parent.TryGetValue(key, out object? value) ? value as string : null;

    private static string? EnglishName(IDictionary<string, object>? parent) => Text(Map(parent, "names"), "en");

    private static double? Number(IDictionary<string, object>? parent, string key) =>
        parent is not null && parent.TryGetValue(key, out object? value) && value is double number && double.IsFinite(number)
            ? number
            : null;

    [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "Loaded IP location database {Path} ({DatabaseType}, built {BuildDate})")]
    private partial void LogDatabaseLoaded(string path, string databaseType, DateTime buildDate);

    [LoggerMessage(EventId = 7102, Level = LogLevel.Warning, Message = "IP location database {Path} is missing; lookups resolve nothing until it appears")]
    private partial void LogDatabaseMissing(string path);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Error, Message = "IP location database {Path} could not be loaded; the previous database stays active")]
    private partial void LogDatabaseLoadFailed(Exception exception, string path);

    [LoggerMessage(EventId = 7104, Level = LogLevel.Warning, Message = "IP location lookup failed on a corrupt database record")]
    private partial void LogLookupFailed(Exception exception);
}
