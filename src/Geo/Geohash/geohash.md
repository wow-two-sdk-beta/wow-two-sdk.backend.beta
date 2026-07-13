# Geo.Geohash

*Base-32 geohash — a short string whose shared prefix length implies proximity.*

Useful as a cheap spatial-index key (group nearby points by prefix) and for proximity bucketing.

| Member | Role |
|---|---|
| `Geohash.Encode(coordinate, precision)` | Coordinate → geohash (precision 1–12; higher = finer) |
| `Geohash.Decode(hash)` | Geohash → `GeoBoundingBox` of the cell (`.Center` for the point) |
| `Geohash.DecodeCenter(hash)` | Geohash → centre `GeoCoordinate` |
| `Geohash.Adjacent(hash, direction)` | Neighbouring cell in one direction |
| `Geohash.Neighbors(hash)` | The 8 surrounding cells (N, NE, E, SE, S, SW, W, NW) |

```csharp
string cell = Geohash.Encode(new GeoCoordinate(41.311, 69.240), 7);  // "tzz…"
var search = new[] { cell }.Concat(Geohash.Neighbors(cell));         // 3×3 proximity window
```

Precision guide: 5 chars ≈ 5 km, 7 ≈ 150 m, 9 ≈ 5 m. Static, pure algorithm.
