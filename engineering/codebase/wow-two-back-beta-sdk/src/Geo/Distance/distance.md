# Geo.Distance

*Great-circle distance, bearing, and destination on a spherical Earth (haversine).*

| Type | Role |
|---|---|
| `IGeoDistanceCalculator` / `GeoDistanceCalculator` | `DistanceMeters` · `Distance(…, unit)` · `InitialBearingDegrees` · `Destination` |
| `DistanceUnit` + `DistanceUnitExtensions` | Meters / Kilometers / Miles / NauticalMiles; `FromMeters` / `ToMeters` |

```csharp
double m   = calc.DistanceMeters(a, b);
double mi  = calc.Distance(a, b, DistanceUnit.Miles);
double brg = calc.InitialBearingDegrees(a, b);            // 0–360° clockwise from north
GeoCoordinate d = calc.Destination(a, 45, 10_000);        // 10 km NE of a
```

Spherical model (IUGG mean radius 6 371 008.8 m) — under ~1% error for terrestrial distances. Register via `AddGeo()`.
