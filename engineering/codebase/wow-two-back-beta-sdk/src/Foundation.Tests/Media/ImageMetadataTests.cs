using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Media.Images;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>EXIF reading from JPEG and PNG, in both byte orders: when, with what, how and where a photo was taken.</summary>
public sealed class ImageMetadataTests
{
    private static readonly IImageService Images = new ServiceCollection().AddImageProcessing().BuildServiceProvider().GetRequiredService<IImageService>();

    [Fact]
    public async Task Jpeg_ShouldReadCameraExposureTimeAndLocation()
    {
        var metadata = await Images.ReadMetadataAsync(new MemoryStream(WithApp1(Encode(SKEncodedImageFormat.Jpeg), Tiff(big: true))));

        metadata.Should().BeEquivalentTo(new
        {
            TakenAt = new DateTime(2026, 9, 28, 14, 30, 0),
            TakenAtOffset = TimeSpan.FromHours(5),
            CameraMake = "Canon",
            CameraModel = "EOS R6",
            LensModel = "RF50mm F1.8",
            Orientation = 6,
            ExposureTime = "1/250",
            FNumber = 2.8,
            Iso = 200,
            FocalLength = 50.0,
            HasLocation = true,
        });
        metadata.Latitude.Should().BeApproximately(41.308333, 1e-5);
        metadata.Longitude.Should().BeApproximately(-69.27, 1e-5, "a west reference turns the longitude negative");
        metadata.Altitude.Should().Be(455);
        metadata.TakenAtWithOffset.Should().Be(new DateTimeOffset(2026, 9, 28, 14, 30, 0, TimeSpan.FromHours(5)));
    }

    [Fact]
    public async Task Png_ShouldReadALittleEndianExifChunk()
    {
        var metadata = await Images.ReadMetadataAsync(new MemoryStream(WithExifChunk(Encode(SKEncodedImageFormat.Png), Tiff(big: false))));

        (metadata.CameraMake, metadata.Iso, metadata.Orientation).Should().Be(("Canon", 200, 6));
        metadata.Latitude.Should().BeApproximately(41.308333, 1e-5);
    }

    [Fact]
    public async Task ImagesWithoutExif_ShouldReadEmpty_AndEditsShouldDropIt()
    {
        var plain = await Images.ReadMetadataAsync(new MemoryStream(Encode(SKEncodedImageFormat.Png)));
        (plain.TakenAt, plain.CameraMake, plain.HasLocation, plain.Orientation).Should().Be((null, null, false, 1));

        var photo = WithApp1(Encode(SKEncodedImageFormat.Jpeg), Tiff(big: true));
        var edited = await Images.EditAsync(new MemoryStream(photo), new ImageEditSpec());
        (await Images.ReadMetadataAsync(new MemoryStream(edited.Content))).HasLocation.Should().BeFalse("re-encoding strips the location");
    }

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(32, 16);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }

    private static byte[] WithApp1(byte[] jpeg, byte[] tiff)
    {
        var segment = new byte[10 + tiff.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(8 + tiff.Length));
        "Exif\0\0"u8.CopyTo(segment.AsSpan(4));
        tiff.CopyTo(segment, 10);
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    /// <summary>Inserts an <c>eXIf</c> chunk before <c>IEND</c>; the reader does not check chunk CRCs.</summary>
    private static byte[] WithExifChunk(byte[] png, byte[] tiff)
    {
        var chunk = new byte[12 + tiff.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)tiff.Length);
        "eXIf"u8.CopyTo(chunk.AsSpan(4));
        tiff.CopyTo(chunk, 8);
        return [.. png[..^12], .. chunk, .. png[^12..]];
    }

    /// <summary>A TIFF block with IFD0, the EXIF sub-IFD and the GPS sub-IFD, laid out as cameras write it.</summary>
    private static byte[] Tiff(bool big)
    {
        List<(ushort Tag, ushort Type, uint Count, byte[] Value)>[] directories =
        [
            [(0x010F, 2, 0, Ascii("Canon")), (0x0110, 2, 0, Ascii("EOS R6")), (0x0112, 3, 1, Short(6, big)), (0x8769, 4, 1, new byte[4]), (0x8825, 4, 1, new byte[4])],
            [(0x829A, 5, 1, Rationals(big, (1, 250))), (0x829D, 5, 1, Rationals(big, (28, 10))), (0x8827, 3, 1, Short(200, big)), (0x9003, 2, 0, Ascii("2026:09:28 14:30:00")), (0x9011, 2, 0, Ascii("+05:00")), (0x920A, 5, 1, Rationals(big, (50, 1))), (0xA434, 2, 0, Ascii("RF50mm F1.8"))],
            [(0x0001, 2, 0, Ascii("N")), (0x0002, 5, 3, Rationals(big, (41, 1), (18, 1), (3000, 100))), (0x0003, 2, 0, Ascii("W")), (0x0004, 5, 3, Rationals(big, (69, 1), (16, 1), (1200, 100))), (0x0005, 1, 1, [0]), (0x0006, 5, 1, Rationals(big, (455, 1)))],
        ];
        var offsets = new int[directories.Length];
        var cursor = 8;
        for (var index = 0; index < directories.Length; index++)
        {
            offsets[index] = cursor;
            cursor += 2 + (12 * directories[index].Count) + 4;
        }

        directories[0][3] = directories[0][3] with { Value = Long((uint)offsets[1], big) };
        directories[0][4] = directories[0][4] with { Value = Long((uint)offsets[2], big) };
        var data = new List<byte>();
        var output = new byte[cursor + directories.Sum(directory => directory.Sum(entry => entry.Value.Length > 4 ? entry.Value.Length + 1 : 0))];
        (big ? "MM\0*"u8 : "II*\0"u8).CopyTo(output);
        Write(output, 4, 8u, big);
        for (var index = 0; index < directories.Length; index++)
        {
            var position = offsets[index];
            Write(output, position, (ushort)directories[index].Count, big);
            position += 2;
            foreach (var (tag, type, count, value) in directories[index])
            {
                Write(output, position, tag, big);
                Write(output, position + 2, type, big);
                Write(output, position + 4, type == 2 ? (uint)value.Length : count, big);
                if (value.Length <= 4)
                {
                    value.CopyTo(output, position + 8);
                }
                else
                {
                    var at = cursor + data.Count;
                    Write(output, position + 8, (uint)at, big);
                    data.AddRange(value);
                    if (data.Count % 2 == 1)
                        data.Add(0);
                }

                position += 12;
            }
        }

        data.CopyTo(output, cursor);
        return output[..(cursor + data.Count)];
    }

    private static byte[] Ascii(string text) => [.. Encoding.ASCII.GetBytes(text), 0];

    private static byte[] Short(ushort value, bool big) => [.. big ? [(byte)(value >> 8), (byte)value] : new[] { (byte)value, (byte)(value >> 8) }, 0, 0];

    private static byte[] Long(uint value, bool big)
    {
        var bytes = new byte[4];
        Write(bytes, 0, value, big);
        return bytes;
    }

    private static byte[] Rationals(bool big, params (uint Numerator, uint Denominator)[] values)
    {
        var bytes = new byte[values.Length * 8];
        for (var index = 0; index < values.Length; index++)
        {
            Write(bytes, index * 8, values[index].Numerator, big);
            Write(bytes, (index * 8) + 4, values[index].Denominator, big);
        }

        return bytes;
    }

    private static void Write(byte[] target, int offset, uint value, bool big)
    {
        if (big)
            BinaryPrimitives.WriteUInt32BigEndian(target.AsSpan(offset), value);
        else
            BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);
    }

    private static void Write(byte[] target, int offset, ushort value, bool big)
    {
        if (big)
            BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset), value);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), value);
    }
}
