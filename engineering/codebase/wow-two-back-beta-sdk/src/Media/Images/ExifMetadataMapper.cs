using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>
/// Maps the EXIF block of a JPEG (APP1), PNG (<c>eXIf</c>) or WebP (<c>EXIF</c>) file to <see cref="ImageMetadataResult"/>.
/// Reads IFD0, the EXIF sub-IFD and the GPS sub-IFD with bounds checks; a malformed block yields what was read so far.
/// </summary>
internal static class ExifMetadataMapper
{
    private const ushort Make = 0x010F, Model = 0x0110, Orientation = 0x0112, Software = 0x0131, ExifPointer = 0x8769, GpsPointer = 0x8825;
    private const ushort ExposureTime = 0x829A, FNumber = 0x829D, Iso = 0x8827, TakenAt = 0x9003, TakenAtOffset = 0x9011, FocalLength = 0x920A, Lens = 0xA434;
    private const ushort LatitudeRef = 0x0001, Latitude = 0x0002, LongitudeRef = 0x0003, Longitude = 0x0004, AltitudeRef = 0x0005, Altitude = 0x0006;

    public static ImageMetadataResult Map(ReadOnlySpan<byte> file)
    {
        var tiff = Locate(file);
        if (tiff.Length < 8)
            return new ImageMetadataResult();

        var big = tiff[0] == (byte)'M';
        var entries = new Dictionary<ushort, object>();
        var visited = new HashSet<int>();
        Read(tiff, big, (int)U32(tiff, 4, big), entries, visited, depth: 0);

        var gps = new Dictionary<ushort, object>();
        if (entries.TryGetValue(GpsPointer, out var gpsOffset) && gpsOffset is uint[] { Length: > 0 } pointer)
            Read(tiff, big, (int)pointer[0], gps, visited, depth: 1);

        return new ImageMetadataResult
        {
            TakenAt = Text(entries, TakenAt) is { } taken && DateTime.TryParseExact(taken, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment) ? moment : null,
            TakenAtOffset = Text(entries, TakenAtOffset) is { Length: 6 } offset && TimeSpan.TryParseExact(offset[1..], @"hh\:mm", CultureInfo.InvariantCulture, out var span)
                ? (offset[0] == '-' ? -span : span)
                : null,
            CameraMake = Text(entries, Make),
            CameraModel = Text(entries, Model),
            LensModel = Text(entries, Lens),
            Software = Text(entries, Software),
            Orientation = entries.TryGetValue(Orientation, out var orientation) && orientation is ushort[] { Length: > 0 } values && values[0] is >= 1 and <= 8 ? values[0] : 1,
            ExposureTime = entries.TryGetValue(ExposureTime, out var exposure) && exposure is (uint, uint)[] { Length: > 0 } fraction && fraction[0].Item2 != 0
                ? Fraction(fraction[0])
                : null,
            FNumber = Rational(entries, FNumber, 0),
            Iso = entries.TryGetValue(Iso, out var iso) && iso is ushort[] { Length: > 0 } speeds ? speeds[0] : null,
            FocalLength = Rational(entries, FocalLength, 0),
            Latitude = Coordinate(gps, Latitude, LatitudeRef, 'S'),
            Longitude = Coordinate(gps, Longitude, LongitudeRef, 'W'),
            Altitude = Rational(gps, Altitude, 0) is { } height
                ? gps.TryGetValue(AltitudeRef, out var below) && below is byte[] { Length: > 0 } flag && flag[0] == 1 ? -height : height
                : null,
        };
    }

    /// <summary>The TIFF structure inside the container, or empty when the file carries no EXIF.</summary>
    private static ReadOnlySpan<byte> Locate(ReadOnlySpan<byte> file)
    {
        if (file.Length > 4 && file[0] == 0xFF && file[1] == 0xD8)
        {
            for (var position = 2; position + 4 <= file.Length && file[position] == 0xFF;)
            {
                var marker = file[position + 1];
                var length = BinaryPrimitives.ReadUInt16BigEndian(file[(position + 2)..]);
                if (marker == 0xE1 && length > 8 && position + 2 + length <= file.Length && file.Slice(position + 4, 6).SequenceEqual("Exif\0\0"u8))
                    return file.Slice(position + 10, length - 8);
                if (marker is 0xDA or 0xD9)
                    break;
                position += 2 + length;
            }
        }
        else if (file.Length > 8 && file[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            for (var position = 8; position + 12 <= file.Length;)
            {
                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(file[position..]);
                if (length < 0 || position + 12 + length > file.Length)
                    break;
                if (file.Slice(position + 4, 4).SequenceEqual("eXIf"u8))
                    return file.Slice(position + 8, length);
                position += 12 + length;
            }
        }
        else if (file.Length > 12 && file[..4].SequenceEqual("RIFF"u8) && file.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            for (var position = 12; position + 8 <= file.Length;)
            {
                var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(file[(position + 4)..]);
                if (length < 0 || position + 8 + length > file.Length)
                    break;
                if (file.Slice(position, 4).SequenceEqual("EXIF"u8))
                {
                    var chunk = file.Slice(position + 8, length);
                    return chunk.StartsWith("Exif\0\0"u8) ? chunk[6..] : chunk;
                }

                position += 8 + length + (length & 1);
            }
        }

        return [];
    }

    private static void Read(ReadOnlySpan<byte> tiff, bool big, int offset, Dictionary<ushort, object> entries, HashSet<int> visited, int depth)
    {
        if (depth > 2 || offset < 8 || offset + 2 > tiff.Length || !visited.Add(offset))
            return;

        var count = U16(tiff, offset, big);
        for (var index = 0; index < count && index < 512; index++)
        {
            var entry = offset + 2 + (index * 12);
            if (entry + 12 > tiff.Length)
                return;

            var tag = U16(tiff, entry, big);
            var type = U16(tiff, entry + 2, big);
            var items = (int)Math.Min(U32(tiff, entry + 4, big), 4096);
            var size = type switch { 1 or 2 or 7 => 1, 3 => 2, 4 or 9 => 4, 5 or 10 => 8, _ => 0 };
            if (size == 0 || items == 0)
                continue;

            var start = size * items <= 4 ? entry + 8 : (int)U32(tiff, entry + 8, big);
            if (start < 0 || start + (size * items) > tiff.Length)
                continue;

            var data = tiff.Slice(start, size * items).ToArray();
            entries[tag] = type switch
            {
                2 => Encoding.ASCII.GetString(data).TrimEnd('\0', ' '),
                3 => Enumerable.Range(0, items).Select(item => U16(data, item * 2, big)).ToArray(),
                4 or 9 => Enumerable.Range(0, items).Select(item => U32(data, item * 4, big)).ToArray(),
                5 or 10 => Enumerable.Range(0, items).Select(item => (U32(data, item * 8, big), U32(data, (item * 8) + 4, big))).ToArray(),
                _ => data,
            };
        }

        if (depth == 0 && entries.TryGetValue(ExifPointer, out var exif) && exif is uint[] { Length: > 0 } pointer)
            Read(tiff, big, (int)pointer[0], entries, visited, depth + 1);
    }

    private static string? Text(Dictionary<ushort, object> entries, ushort tag)
        => entries.TryGetValue(tag, out var value) && value is string { Length: > 0 } text ? text : null;

    private static double? Rational(Dictionary<ushort, object> entries, ushort tag, int index)
        => entries.TryGetValue(tag, out var value) && value is (uint Numerator, uint Denominator)[] rationals && rationals.Length > index && rationals[index].Denominator != 0
            ? (double)rationals[index].Numerator / rationals[index].Denominator
            : null;

    private static double? Coordinate(Dictionary<ushort, object> gps, ushort tag, ushort reference, char negative)
    {
        if (Rational(gps, tag, 0) is not { } degrees)
            return null;

        var value = degrees + ((Rational(gps, tag, 1) ?? 0) / 60) + ((Rational(gps, tag, 2) ?? 0) / 3600);
        return Text(gps, reference) is { Length: > 0 } direction && char.ToUpperInvariant(direction[0]) == negative ? -value : value;
    }

    private static string Fraction((uint Numerator, uint Denominator) value)
        => value.Numerator == 1 || value.Numerator == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{value.Numerator}/{value.Denominator}")
            : value.Numerator >= value.Denominator
                ? ((double)value.Numerator / value.Denominator).ToString("0.##", CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"1/{Math.Round((double)value.Denominator / value.Numerator)}");

    private static ushort U16(ReadOnlySpan<byte> data, int offset, bool big)
        => big ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..]) : BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    private static uint U32(ReadOnlySpan<byte> data, int offset, bool big)
        => big ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
}
