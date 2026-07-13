using System.Text;
using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.Geohash;

/// <summary>
/// Geohash encode/decode and neighbour lookup — the public-domain base-32 geohash scheme that maps a
/// coordinate to a short string whose shared prefix length implies proximity. Useful as a cheap spatial
/// index key and for proximity bucketing. Pure algorithm, no dependencies.
/// </summary>
public static class Geohash
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
    private static readonly int[] Bits = [16, 8, 4, 2, 1];

    /// <summary>Encodes a coordinate to a geohash string of the given precision (characters).</summary>
    /// <param name="coordinate">The coordinate to encode.</param>
    /// <param name="precision">The number of geohash characters, 1–12. Higher is more precise. Default 9 (≈ 5 m).</param>
    /// <returns>The geohash string.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="precision"/> is outside 1–12.</exception>
    public static string Encode(GeoCoordinate coordinate, int precision = 9)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentOutOfRangeException.ThrowIfLessThan(precision, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, 12);

        double latMin = -90, latMax = 90, lonMin = -180, lonMax = 180;
        var geohash = new StringBuilder(precision);
        var even = true;
        var bit = 0;
        var value = 0;

        while (geohash.Length < precision)
        {
            if (even)
            {
                var mid = (lonMin + lonMax) / 2;
                if (coordinate.Longitude >= mid) { value |= Bits[bit]; lonMin = mid; }
                else { lonMax = mid; }
            }
            else
            {
                var mid = (latMin + latMax) / 2;
                if (coordinate.Latitude >= mid) { value |= Bits[bit]; latMin = mid; }
                else { latMax = mid; }
            }

            even = !even;

            if (bit < 4)
            {
                bit++;
            }
            else
            {
                geohash.Append(Base32[value]);
                bit = 0;
                value = 0;
            }
        }

        return geohash.ToString();
    }

    /// <summary>Decodes a geohash to the bounding box of the cell it represents.</summary>
    /// <param name="geohash">The geohash string (case-insensitive).</param>
    /// <returns>The cell's bounding box; its <see cref="GeoBoundingBox.Center"/> is the decoded point.</returns>
    /// <exception cref="ArgumentException"><paramref name="geohash"/> is empty or contains a non-base-32 character.</exception>
    public static GeoBoundingBox Decode(string geohash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geohash);

        double latMin = -90, latMax = 90, lonMin = -180, lonMax = 180;
        var even = true;

        foreach (var c in geohash.ToLowerInvariant())
        {
            var index = Base32.IndexOf(c);
            if (index < 0) throw new ArgumentException($"'{c}' is not a valid geohash character.", nameof(geohash));

            foreach (var mask in Bits)
            {
                if (even)
                {
                    var mid = (lonMin + lonMax) / 2;
                    if ((index & mask) != 0) lonMin = mid; else lonMax = mid;
                }
                else
                {
                    var mid = (latMin + latMax) / 2;
                    if ((index & mask) != 0) latMin = mid; else latMax = mid;
                }

                even = !even;
            }
        }

        return new GeoBoundingBox(latMin, lonMin, latMax, lonMax);
    }

    /// <summary>Decodes a geohash to the centre coordinate of its cell.</summary>
    /// <param name="geohash">The geohash string (case-insensitive).</param>
    /// <returns>The cell centre.</returns>
    public static GeoCoordinate DecodeCenter(string geohash) => Decode(geohash).Center;

    /// <summary>Returns the geohash of the cell adjacent to <paramref name="geohash"/> in the given direction.</summary>
    /// <param name="geohash">The source geohash (case-insensitive).</param>
    /// <param name="direction">The direction of the neighbour.</param>
    /// <returns>The adjacent geohash of the same length.</returns>
    /// <exception cref="ArgumentException"><paramref name="geohash"/> is empty.</exception>
    public static string Adjacent(string geohash, GeohashDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geohash);

        var hash = geohash.ToLowerInvariant();
        var last = hash[^1];
        var prefix = hash[..^1];
        var even = hash.Length % 2 == 0;

        var (neighbors, borders) = Table(direction, even);

        if (borders.Contains(last, StringComparison.Ordinal) && prefix.Length > 0)
            prefix = Adjacent(prefix, direction);

        var neighborIndex = neighbors.IndexOf(last);
        return prefix + Base32[neighborIndex];
    }

    /// <summary>Returns the eight geohashes surrounding <paramref name="geohash"/> in the order N, NE, E, SE, S, SW, W, NW.</summary>
    /// <param name="geohash">The source geohash.</param>
    /// <returns>The eight neighbouring geohashes.</returns>
    public static IReadOnlyList<string> Neighbors(string geohash)
    {
        var north = Adjacent(geohash, GeohashDirection.North);
        var south = Adjacent(geohash, GeohashDirection.South);
        var east = Adjacent(geohash, GeohashDirection.East);
        var west = Adjacent(geohash, GeohashDirection.West);

        return
        [
            north,
            Adjacent(north, GeohashDirection.East),
            east,
            Adjacent(south, GeohashDirection.East),
            south,
            Adjacent(south, GeohashDirection.West),
            west,
            Adjacent(north, GeohashDirection.West),
        ];
    }

    // Adjacency lookup tables (public-domain geohash algorithm). Odd-length tables are the even tables of
    // the rotated direction: top.odd = right.even, bottom.odd = left.even, right.odd = top.even, left.odd = bottom.even.
    private static (string Neighbors, string Borders) Table(GeohashDirection direction, bool even)
    {
        const string TopN = "p0r21436x8zb9dcf5h7kjnmqesgutwvy";
        const string TopB = "prxz";
        const string BottomN = "14365h7k9dcfesgujnmqp0r2twvyx8zb";
        const string BottomB = "028b";
        const string RightN = "bc01fg45238967deuvhjyznpkmstqrwx";
        const string RightB = "bcfguvyz";
        const string LeftN = "238967debc01fg45kmstqrwxuvhjyznp";
        const string LeftB = "0145hjnp";

        return even
            ? direction switch
            {
                GeohashDirection.North => (TopN, TopB),
                GeohashDirection.South => (BottomN, BottomB),
                GeohashDirection.East => (RightN, RightB),
                GeohashDirection.West => (LeftN, LeftB),
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            }
            : direction switch
            {
                GeohashDirection.North => (RightN, RightB),
                GeohashDirection.South => (LeftN, LeftB),
                GeohashDirection.East => (TopN, TopB),
                GeohashDirection.West => (BottomN, BottomB),
                _ => throw new ArgumentOutOfRangeException(nameof(direction)),
            };
    }
}
