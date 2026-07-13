# Geo.GeoJson

*RFC 7946 GeoJSON read/write — Point, LineString, Polygon, Feature, FeatureCollection.*

| Type | Role |
|---|---|
| `GeoPosition` | `[longitude, latitude(, altitude)]` position; `FromCoordinate` / `ToCoordinate` bridge to `GeoCoordinate` |
| `GeoJsonGeometry` + `GeoJsonPoint` / `GeoJsonLineString` / `GeoJsonPolygon` | Geometry object model |
| `GeoJsonFeature` / `GeoJsonFeatureCollection` | Feature (geometry + id + raw `properties`) and collection |
| `GeoJsonSerializer` | `Serialize(...)` / `ParseGeometry` / `ParseFeature` / `ParseFeatureCollection` |

```csharp
var pt = new GeoJsonPoint(GeoPosition.FromCoordinate(coord));
string json = GeoJsonSerializer.Serialize(pt);            // {"type":"Point","coordinates":[69.24,41.311]}
var fc = GeoJsonSerializer.ParseFeatureCollection(body);
```

- **Position order is longitude-first** (RFC 7946) — the reverse of `GeoCoordinate`.
- Feature `properties` round-trip as raw `JsonElement` values (cloned, safe to keep after parse).
- Hand-rolled over `Utf8JsonWriter`/`JsonDocument` — no external GeoJSON dependency. MultiPoint/MultiLineString/MultiPolygon/GeometryCollection can be added when a consumer needs them.
