using System.Buffers.Binary;
using System.Collections;
using System.Net;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Geo.IpLocation;

/// <summary>Writes a minimal IPv4 MaxMind DB file so broker tests run without downloaded data.</summary>
/// <remarks>Implements the MMDB 2.0 layout: a 24-bit search tree, a 16-byte separator, the data section and metadata.</remarks>
internal static class MmdbTestDatabaseWriter
{
    private const int RecordBits = 24;

    /// <summary>Writes <paramref name="networks"/> to <paramref name="path"/>; networks must not overlap.</summary>
    public static void Write(string path, IReadOnlyList<(string Cidr, IDictionary<string, object> Data)> networks)
    {
        var data = new List<byte>();
        var nodes = new List<Record[]> { NewNode() };
        foreach ((string cidr, IDictionary<string, object> value) in networks)
        {
            int offset = data.Count;
            Encode(data, value);
            Insert(nodes, cidr, offset);
        }

        int nodeCount = nodes.Count;
        var file = new List<byte>();
        foreach (Record[] node in nodes)
        {
            foreach (Record record in node)
            {
                uint resolved = record.Kind switch
                {
                    RecordKind.Node => (uint)record.Value,
                    RecordKind.Data => (uint)(nodeCount + 16 + record.Value),
                    _ => (uint)nodeCount,
                };
                file.Add((byte)(resolved >> 16));
                file.Add((byte)(resolved >> 8));
                file.Add((byte)resolved);
            }
        }

        file.AddRange(new byte[16]);
        file.AddRange(data);
        file.AddRange([0xAB, 0xCD, 0xEF]);
        file.AddRange(Encoding.ASCII.GetBytes("MaxMind.com"));
        Encode(file, new Dictionary<string, object>
        {
            ["binary_format_major_version"] = (ushort)2,
            ["binary_format_minor_version"] = (ushort)0,
            ["build_epoch"] = 1_800_000_000UL,
            ["database_type"] = "WoW2-Test-Country",
            ["description"] = new Dictionary<string, object> { ["en"] = "Test database" },
            ["ip_version"] = (ushort)4,
            ["languages"] = new List<object> { "en" },
            ["node_count"] = (uint)nodeCount,
            ["record_size"] = (ushort)RecordBits,
        });
        File.WriteAllBytes(path, [.. file]);
    }

    private static void Insert(List<Record[]> nodes, string cidr, int dataOffset)
    {
        string[] parts = cidr.Split('/');
        uint prefix = BinaryPrimitives.ReadUInt32BigEndian(IPAddress.Parse(parts[0]).GetAddressBytes());
        int length = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        int current = 0;
        for (int depth = 0; depth < length; depth++)
        {
            int bit = (int)((prefix >> (31 - depth)) & 1);
            if (depth == length - 1)
            {
                nodes[current][bit] = new Record { Kind = RecordKind.Data, Value = dataOffset };
                return;
            }

            if (nodes[current][bit].Kind != RecordKind.Node)
            {
                nodes.Add(NewNode());
                nodes[current][bit] = new Record { Kind = RecordKind.Node, Value = nodes.Count - 1 };
            }

            current = nodes[current][bit].Value;
        }
    }

    private static Record[] NewNode() => [new Record { Kind = RecordKind.Empty, Value = 0 }, new Record { Kind = RecordKind.Empty, Value = 0 }];

    private static void Encode(List<byte> output, object value)
    {
        switch (value)
        {
            case string text:
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                Control(output, 2, bytes.Length);
                output.AddRange(bytes);
                break;
            case double number:
                Control(output, 3, 8);
                byte[] raw = new byte[8];
                BinaryPrimitives.WriteDoubleBigEndian(raw, number);
                output.AddRange(raw);
                break;
            case ushort small:
                Unsigned(output, 5, small);
                break;
            case uint medium:
                Unsigned(output, 6, medium);
                break;
            case ulong large:
                Unsigned(output, 9, large);
                break;
            case IDictionary<string, object> map:
                Control(output, 7, map.Count);
                foreach ((string key, object item) in map)
                {
                    Encode(output, key);
                    Encode(output, item);
                }

                break;
            case IList list:
                Control(output, 11, list.Count);
                foreach (object item in list)
                {
                    Encode(output, item);
                }

                break;
            default:
                throw new NotSupportedException($"MMDB test values cannot be {value.GetType().Name}.");
        }
    }

    private static void Unsigned(List<byte> output, int type, ulong value)
    {
        byte[] raw = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(raw, value);
        int skip = 0;
        while (skip < 8 && raw[skip] == 0)
        {
            skip++;
        }

        Control(output, type, 8 - skip);
        output.AddRange(raw[skip..]);
    }

    private static void Control(List<byte> output, int type, int size)
    {
        bool extended = type > 7;
        int first = extended ? 0 : type << 5;
        if (size < 29)
        {
            output.Add((byte)(first | size));
            AddExtendedType(output, extended, type);
        }
        else if (size < 285)
        {
            output.Add((byte)(first | 29));
            AddExtendedType(output, extended, type);
            output.Add((byte)(size - 29));
        }
        else
        {
            output.Add((byte)(first | 30));
            AddExtendedType(output, extended, type);
            int rest = size - 285;
            output.Add((byte)(rest >> 8));
            output.Add((byte)rest);
        }
    }

    private static void AddExtendedType(List<byte> output, bool extended, int type)
    {
        if (extended)
        {
            output.Add((byte)(type - 7));
        }
    }

    private enum RecordKind
    {
        Empty,
        Node,
        Data,
    }

    private readonly record struct Record
    {
        public required RecordKind Kind { get; init; }

        public required int Value { get; init; }
    }
}
