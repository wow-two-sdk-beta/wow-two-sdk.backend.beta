# Geo.Coordinates

*Validated geographic point + bounding box.*

| Type | Role |
|---|---|
| `GeoCoordinate` | WGS-84 lat/lon (+ optional altitude), validated on construction; `LatitudeRadians`/`LongitudeRadians` helpers |
| `GeoBoundingBox` | Axis-aligned lat/lon rectangle: `Contains`, `Extend`, `Center`, `FromCoordinates(...)` |

```csharp
var p = new GeoCoordinate(41.311, 69.240);          // throws if out of [-90,90]/[-180,180]
var box = GeoBoundingBox.FromCoordinates(points);   // tightest enclosing rectangle
bool inside = box.Contains(p);
```

Latitude-first (spoken order). For GeoJSON's longitude-first positions use `Geo.GeoJson.GeoPosition`.
