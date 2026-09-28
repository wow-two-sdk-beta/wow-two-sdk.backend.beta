using System.Buffers.Binary;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Maps one face of a TrueType collection (<c>.ttc</c>) to a standalone font file, which PDFsharp can embed.</summary>
internal static class TrueTypeCollectionMapper
{
    /// <summary>Whether <paramref name="font"/> is a collection.</summary>
    public static bool IsCollection(ReadOnlySpan<byte> font) => font.Length >= 12 && font[..4].SequenceEqual("ttcf"u8);

    /// <summary>The face at <paramref name="index"/> as a standalone font: its table directory plus each table it names.</summary>
    public static byte[] Extract(byte[] collection, int index)
    {
        var faces = BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(8));
        if (index < 0 || index >= faces)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"The collection holds {faces} faces.");

        var directory = (int)BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(12 + (4 * index)));
        var tables = BinaryPrimitives.ReadUInt16BigEndian(collection.AsSpan(directory + 4));
        var header = 12 + (16 * tables);
        var size = header + Enumerable.Range(0, tables).Sum(table => Align((int)Length(collection, directory, table)));
        var font = new byte[size];
        collection.AsSpan(directory, header).CopyTo(font);

        var position = header;
        for (var table = 0; table < tables; table++)
        {
            var record = directory + 12 + (16 * table);
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(record + 8));
            var length = (int)Length(collection, directory, table);
            collection.AsSpan(offset, length).CopyTo(font.AsSpan(position));
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(12 + (16 * table) + 8), (uint)position);
            position += Align(length);
        }

        return font;
    }

    private static uint Length(byte[] collection, int directory, int table)
        => BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(directory + 12 + (16 * table) + 12));

    private static int Align(int length) => (length + 3) & ~3;
}
