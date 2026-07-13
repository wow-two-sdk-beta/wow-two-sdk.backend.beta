# Geo

*Geographic primitives — coordinates, great-circle distance/bearing, geohash indexing, and GeoJSON I/O. Pure C#, zero dependencies.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Geo`. This is the dependency-free foundation of the Geo vector; heavier adapters (NetTopologySuite geometry ops, MaxMind IP→geo) plug in later without changing these types.

## Components

| Folder | Surface | Role |
|---|---|---|
| `Coordinates/` | `GeoCoordinate`, `GeoBoundingBox` | Validated WGS-84 point + axis-aligned bbox (contains/extend/enclose) |
| `Distance/` | `IGeoDistanceCalculator` / `GeoDistanceCalculator`, `DistanceUnit` | Haversine distance, initial bearing, destination point; unit conversion |
| `Geohash/` | `Geohash` (static), `GeohashDirection` | Encode/decode + adjacent/neighbours for spatial-index keys & proximity |
| `GeoJson/` | `GeoJsonSerializer`, `GeoJson*` types, `GeoPosition` | RFC 7946 Point/LineString/Polygon/Feature(Collection) read+write |

## Quickstart

```csharp
builder.Services.AddGeo();   // registers IGeoDistanceCalculator

var a = new GeoCoordinate(41.311, 69.240);   // Tashkent
var b = new GeoCoordinate(40.409, 49.867);   // Baku
double km = calc.Distance(a, b, DistanceUnit.Kilometers);
double bearing = calc.InitialBearingDegrees(a, b);
GeoCoordinate onward = calc.Destination(a, bearingDegrees: 90, distanceMeters: 5000);

string cell = Geohash.Encode(a, precision: 7);          // proximity key
IReadOnlyList<string> ring = Geohash.Neighbors(cell);   // 8 surrounding cells

string json = GeoJsonSerializer.Serialize(new GeoJsonPoint(GeoPosition.FromCoordinate(a)));
GeoJsonFeatureCollection fc = GeoJsonSerializer.ParseFeatureCollection(body);
```

## Notes

- **Order matters:** `GeoCoordinate` is latitude-first (spoken order); GeoJSON `GeoPosition` is longitude-first (RFC 7946). Convert with `GeoPosition.FromCoordinate` / `ToCoordinate`.
- Distance uses a spherical Earth (haversine, IUGG mean radius) — < 1% error for terrestrial spans. Use an ellipsoidal model for survey-grade precision.
- Everything is immutable + thread-safe; `Geohash`/`GeoJsonSerializer` are static.

## Roadmap (adapters, not yet built)

- `Geo.Nts` — NetTopologySuite geometry (intersections, buffers, spatial predicates) + EF Core spatial columns.
- `Geo.MaxMind` — `IIpGeolocationProvider` over MaxMind GeoIP2 (IP → coordinate/country).

## See also

`Coordinates/coordinates.md` · `Distance/distance.md` · `Geohash/geohash.md` · `GeoJson/geojson.md`
