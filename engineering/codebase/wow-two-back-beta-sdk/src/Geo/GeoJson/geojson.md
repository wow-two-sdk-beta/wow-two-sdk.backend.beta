# Geo.GeoJson

*RFC 7946 GeoJSON read/write — Point, LineString, Polygon, Feature, FeatureCollection.*

`IGeoJsonSerializer` and `GeoJsonSerializer` live in `Serializers/`, namespace
`WoW.Two.Sdk.Backend.Beta.Geo.GeoJson.Serializers`.

| Type | Role |
|---|---|
| `GeoPosition` | `[longitude, latitude(, altitude)]` position; `FromCoordinate` / `ToCoordinate` bridge to `GeoCoordinate` |
| `GeoJsonGeometry` + `GeoJsonPoint` / `GeoJsonLineString` / `GeoJsonPolygon` | Geometry object model |
| `GeoJsonFeature` / `GeoJsonFeatureCollection` | Feature (geometry + raw string/number `id` + raw `properties`) and collection |
| `GeoJsonSerializer` | `Serialize(...)` / `ParseGeometry` / `ParseFeature` / `ParseFeatureCollection` |

```csharp
using WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;
using WoW.Two.Sdk.Backend.Beta.Geo.GeoJson.Serializers;

IGeoJsonSerializer serializer = new GeoJsonSerializer();
var pt = new GeoJsonPoint { Position = GeoPosition.FromCoordinate(coord) };
string json = serializer.Serialize(pt);                  // {"type":"Point","coordinates":[69.24,41.311]}
var fc = serializer.ParseFeatureCollection(body);
```

- **Position order is longitude-first** (RFC 7946) — the reverse of `GeoCoordinate`.
- Feature `properties` round-trip as raw `JsonElement` values (cloned, safe to keep after parse).
- Feature `id` preserves its JSON string or number kind; other JSON kinds are rejected.
- Parsers require the matching root `type` and required Feature/FeatureCollection members.
- Positions support longitude, latitude and optional altitude; extra elements are rejected.
- `bbox` and foreign members are discarded because this model does not represent them.
- Geometry cardinality, polygon closure and ring winding remain caller validation.
- Hand-rolled over `Utf8JsonWriter`/`JsonDocument` — no external GeoJSON dependency. MultiPoint/MultiLineString/MultiPolygon/GeometryCollection can be added when a consumer needs them.
