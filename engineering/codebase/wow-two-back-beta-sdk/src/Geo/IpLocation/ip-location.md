# IP location

> Country, and optionally city, for an IP address from a MaxMind DB file — free now, paid later, one contract.

```csharp
// Free: DB-IP Lite, downloaded monthly into a writable volume.
builder.Services.AddDbIpLiteIpLocation(options => options.DatabaseDirectory = "/data/geo");

// Or a provisioned file: GeoLite2, a paid GeoIP2 database, or a DB-IP file baked into the image.
builder.Services.AddMmdbIpLocation(options => options.DatabasePath = "/data/geo/GeoIP2-Country.mmdb");

// Request path: in-memory and synchronous.
IpLocationModel? location = broker.Locate(httpContext.Connection.RemoteIpAddress!);
string? country = location?.CountryCode;
```

## Behavior

- `IIpLocationBroker.Locate` returns `null` for private, reserved and unknown addresses.
- The broker loads the file into memory and reloads it when its write time changes.
- A missing or corrupt file resolves nothing, logs, and keeps the last good database.
- An IPv4-only database returns `null` for IPv6 clients; IPv4-mapped IPv6 addresses resolve.
- `RemoteIpAddress` is the real client only when forwarded headers come from trusted proxies.

## DB-IP Lite refresh

- `AddDbIpLiteIpLocation` downloads `dbip-{edition}-lite-{yyyy-MM}.mmdb.gz` at startup and each `RefreshInterval`.
- Before the current month is published, the previous month's release fills the gap.
- A release replaces the local file only after it opens as a valid database, within `MaxDatabaseBytes`.
- `Edition = City` adds subdivision, city and coordinates; the download is far larger.

## Licensing

- DB-IP Lite data is licensed CC BY 4.0. Show **IP Geolocation by DB-IP** linking to <https://db-ip.com>.
- GeoLite2 needs a free MaxMind account and licence key; GeoIP2 is paid. Both use `AddMmdbIpLocation`.
- The reader is `MaxMind.Db` (Apache-2.0). No database file ships inside the SDK package.
