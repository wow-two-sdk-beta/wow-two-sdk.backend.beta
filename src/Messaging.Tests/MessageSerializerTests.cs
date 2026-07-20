using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Grade of a shipment — an enum member of <see cref="SerializerPayload"/>, so every serializer's enum policy is exercised by the round trip.</summary>
public enum ShipmentGrade
{
    Standard = 0,
    Express = 1,
    Overnight = 2,
}

/// <summary>
/// The shared round-trip contract: a record (positional, so there is a constructor to bind), a collection, a nullable
/// left null, an enum, and a <see cref="DateTimeOffset"/> carrying a non-UTC offset.
/// </summary>
/// <remarks>
/// Deliberately carries no <c>[MessagePackObject]</c>/<c>[Key]</c> annotation — the contractless resolver has to bind it
/// as-is, which is the promise that lets a contract survive the swap away from System.Text.Json.
/// </remarks>
public sealed record SerializerPayload(
    string Name,
    int Count,
    ShipmentGrade Grade,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> Tags,
    int? Optional);

/// <summary>A contract given an explicit CloudEvents type token through <c>MapMessageType</c>.</summary>
public sealed record CheckoutCompleted(string OrderId, decimal Total) : IEvent;

/// <summary>
/// The three shipped <see cref="IMessageSerializer"/> implementations: round trip, the exact content type each stamps,
/// the CloudEvents attribute contract and its non-.NET interop path, MessagePack's contractless/no-typeless posture,
/// and the empty-payload behaviour — all three return null, per the interface contract.
/// </summary>
public sealed class MessageSerializerTests
{
    private static readonly DateTimeOffset Occurred = new(2026, 7, 19, 14, 30, 0, TimeSpan.FromHours(5));

    private static SerializerPayload Sample => new(
        Name: "crate-7",
        Count: 3,
        Grade: ShipmentGrade.Express,
        OccurredAt: Occurred,
        Tags: ["fragile", "heavy"],
        Optional: null);

    private static CloudEventsMessageSerializer CloudEvents(CloudEventsSerializerOptions? options = null)
        => new(new DefaultMessageTypeResolver(new MessageTypeRegistry()), options);

    // ---- round trip -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("stj")]
    [InlineData("msgpack")]
    [InlineData("cloudevents")]
    public void Every_serializer_round_trips_records_collections_nullables_enums_and_offsets(string name)
    {
        var serializer = Resolve(name);

        var bytes = serializer.Serialize(Sample, typeof(SerializerPayload));
        var back = serializer.Deserialize(bytes, typeof(SerializerPayload)).Should().BeOfType<SerializerPayload>().Subject;

        back.Name.Should().Be("crate-7");
        back.Count.Should().Be(3);
        back.Grade.Should().Be(ShipmentGrade.Express);
        back.Tags.Should().Equal("fragile", "heavy"); // record equality would compare the list by reference — assert it structurally
        back.Optional.Should().BeNull();

        // Offset is part of the value, not decoration: a serializer that normalizes to UTC silently rewrites the
        // business meaning of "14:30 local" for every consumer downstream.
        back.OccurredAt.Should().Be(Occurred);
        back.OccurredAt.Offset.Should().Be(TimeSpan.FromHours(5));
    }

    [Theory]
    [InlineData("stj", "application/json")]
    [InlineData("msgpack", "application/x-msgpack")]
    [InlineData("cloudevents", "application/cloudevents+json")]
    public void Content_type_is_the_exact_token_the_receiver_selects_on(string name, string expected)
    {
        // The adapter stamps this verbatim into wt-content-type and the consumer picks its deserializer by matching
        // it, so a drifted spelling is a silent cross-service break, not a compile error.
        Resolve(name).ContentType.Should().Be(expected);
    }

    // ---- the additive guarantee -------------------------------------------------------------------------------

    [Fact]
    public void SystemTextJson_output_is_byte_identical_to_the_pre_batch_contract()
    {
        var bytes = new SystemTextJsonMessageSerializer().Serialize(Sample, typeof(SerializerPayload));

        // Pinned literally, because "the new serializers are additive" is the claim the whole batch rests on: adding
        // them must not have moved the default wire format by a single byte. camelCase names · numeric enum (the Web
        // defaults add no string-enum converter) · offset preserved · null property omitted (WhenWritingNull).
        Encoding.UTF8.GetString(bytes).Should().Be(
            """{"name":"crate-7","count":3,"grade":1,"occurredAt":"2026-07-19T14:30:00+05:00","tags":["fragile","heavy"]}""");
    }

    [Fact]
    public void CloudEvents_data_section_is_the_untouched_SystemTextJson_body()
    {
        var stj = new SystemTextJsonMessageSerializer().Serialize(Sample, typeof(SerializerPayload));

        var envelope = CloudEvents().Serialize(Sample, typeof(SerializerPayload));

        // The wrapper adds attributes around the body; it must not re-encode the body under a different JSON policy,
        // or a consumer reading `data` gets different casing/null handling than the same contract sent as plain JSON.
        var data = JsonDocument.Parse(envelope).RootElement.GetProperty("data").GetRawText();
        data.Should().Be(Encoding.UTF8.GetString(stj));
    }

    // ---- CloudEvents attribute contract -----------------------------------------------------------------------

    [Fact]
    public void CloudEvents_emits_all_four_required_attributes()
    {
        var bytes = CloudEvents(new CloudEventsSerializerOptions { Source = new Uri("urn:acme:checkout") })
            .Serialize(Sample, typeof(SerializerPayload));

        var root = JsonDocument.Parse(bytes).RootElement;

        // id · source · type · specversion are REQUIRED by CloudEvents 1.0; a consumer's spec-compliant parser rejects
        // the event outright if any is missing, so absence is a hard interop failure rather than a lossy one.
        root.GetProperty("specversion").GetString().Should().Be("1.0");
        root.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("source").GetString().Should().Be("urn:acme:checkout");
        root.GetProperty("datacontenttype").GetString().Should().Be("application/json");

        // An unregistered contract falls back to the assembly-qualified name, so `type` leaks assembly + version and
        // is worthless as an interop token. Not a defect — it is the documented resolver fallback — but it is why the
        // next test exists: MapMessageType is what makes this attribute fit for a non-.NET subscriber.
        root.GetProperty("type").GetString().Should().Be(typeof(SerializerPayload).AssemblyQualifiedName);
    }

    [Fact]
    public void MapMessageType_drives_the_cloudevents_type_attribute()
    {
        // The reverse-DNS token a non-.NET consumer subscribes on. Without this the `type` is a CLR full name, which
        // leaks the namespace and breaks the moment the class moves.
        var registry = new ServiceCollection()
            .MapMessageType<CheckoutCompleted>("com.acme.checkout.completed")
            .BuildServiceProvider()
            .GetRequiredService<MessageTypeRegistry>();
        var serializer = new CloudEventsMessageSerializer(new DefaultMessageTypeResolver(registry));

        var bytes = serializer.Serialize(new CheckoutCompleted("ord-1", 12.5m), typeof(CheckoutCompleted));

        JsonDocument.Parse(bytes).RootElement.GetProperty("type").GetString().Should().Be("com.acme.checkout.completed");
    }

    [Fact]
    public void IdFactory_drives_the_cloudevents_id()
    {
        // source + id is the consumer's duplicate-detection key. Pointed at a business key it dedupes; left default it
        // is a fresh GUID the SDK never sees again, and dedupe silently never fires.
        var serializer = CloudEvents(new CloudEventsSerializerOptions
        {
            IdFactory = (body, _) => "order-" + ((CheckoutCompleted)body).OrderId,
        });

        var bytes = serializer.Serialize(new CheckoutCompleted("ord-42", 9m), typeof(CheckoutCompleted));

        JsonDocument.Parse(bytes).RootElement.GetProperty("id").GetString().Should().Be("order-ord-42");
    }

    [Fact]
    public void Default_cloudevents_id_is_unique_per_serialize()
    {
        var serializer = CloudEvents();

        var first = IdOf(serializer.Serialize(Sample, typeof(SerializerPayload)));
        var second = IdOf(serializer.Serialize(Sample, typeof(SerializerPayload)));

        first.Should().NotBe(second); // no IdFactory → a fresh GUID, so the same body is never two copies of one event
        static string? IdOf(byte[] bytes) => JsonDocument.Parse(bytes).RootElement.GetProperty("id").GetString();
    }

    // ---- CloudEvents interop (the entire point of the format) -------------------------------------------------

    /// <summary>
    /// A CloudEvent as a non-.NET producer actually writes one: attributes in arbitrary order, extension attributes
    /// this SDK knows nothing about (including a nested object), and the payload in <c>data_base64</c>.
    /// </summary>
    private const string HandWrittenForeignEvent = """
        {
          "data_base64": "eyJuYW1lIjoiZnJvbS1nbyIsImNvdW50Ijo5LCJncmFkZSI6Miwib2NjdXJyZWRBdCI6IjIwMjYtMDctMTlUMTQ6MzA6MDArMDU6MDAiLCJ0YWdzIjpbImltcG9ydGVkIl19",
          "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
          "type": "com.acme.orders.placed",
          "specversion": "1.0",
          "partitionkey": "tenant-42",
          "extensionobject": { "nested": { "deep": [1, 2, 3] } },
          "id": "9f6c1b1e-0f2a-4d3c-8b7a-1c2d3e4f5a6b",
          "source": "/acme/checkout/go-service",
          "time": "2026-07-19T09:15:00Z",
          "datacontenttype": "application/json"
        }
        """;

    [Fact]
    public void Parses_a_hand_written_non_dotnet_cloudevent_with_shuffled_attributes_and_unknown_extensions()
    {
        var bytes = Encoding.UTF8.GetBytes(HandWrittenForeignEvent);

        var back = CloudEvents().Deserialize(bytes, typeof(SerializerPayload))
            .Should().BeOfType<SerializerPayload>().Subject;

        // A round trip against our own writer proves nothing here: it would emit `data`, in our order, with no
        // extensions. Interop means surviving a document this SDK did not write.
        back.Name.Should().Be("from-go");
        back.Count.Should().Be(9);
        back.Grade.Should().Be(ShipmentGrade.Overnight);
        back.Tags.Should().Equal("imported");
        back.OccurredAt.Should().Be(Occurred);
    }

    [Fact]
    public void Data_base64_is_handed_over_raw_when_the_contract_is_byte_array()
    {
        var bytes = Encoding.UTF8.GetBytes(HandWrittenForeignEvent);

        var back = CloudEvents().Deserialize(bytes, typeof(byte[])).Should().BeOfType<byte[]>().Subject;

        // data_base64 is the spec's carrier for payloads that are not JSON. A byte[] contract must get the decoded
        // bytes, not a JSON re-parse of them.
        Encoding.UTF8.GetString(back).Should().StartWith("""{"name":"from-go""");
    }

    [Fact]
    public void Cloudevent_with_no_data_payload_yields_null()
    {
        var bytes = Encoding.UTF8.GetBytes(
            """{"specversion":"1.0","id":"1","source":"/acme","type":"com.acme.ping","time":"2026-07-19T09:15:00Z"}""");

        CloudEvents().Deserialize(bytes, typeof(SerializerPayload)).Should().BeNull();
    }

    [Fact]
    public void Non_object_cloudevents_payload_is_rejected()
    {
        var bytes = Encoding.UTF8.GetBytes("\"not-an-envelope\"");

        var parse = () => CloudEvents().Deserialize(bytes, typeof(SerializerPayload));

        parse.Should().Throw<JsonException>(); // structured mode is a JSON object by definition
    }

    // ---- MessagePack: contractless, and no typeless resolver anywhere near it ----------------------------------

    [Fact]
    public void Unannotated_record_serializes_under_the_contractless_resolver()
    {
        var serializer = new MessagePackMessageSerializer();

        var bytes = serializer.Serialize(Sample, typeof(SerializerPayload));

        typeof(SerializerPayload).GetCustomAttributes(typeof(MessagePackObjectAttribute), inherit: false)
            .Should().BeEmpty(); // the premise: no annotation on the contract
        bytes.Should().NotBeEmpty();
        serializer.Deserialize(bytes, typeof(SerializerPayload)).Should().BeOfType<SerializerPayload>();
    }

    [Fact]
    public void MessagePack_payload_carries_no_clr_type_name()
    {
        var bytes = new MessagePackMessageSerializer().Serialize(Sample, typeof(SerializerPayload));

        // A typeless payload embeds the assembly-qualified name and the deserializer instantiates whatever it names —
        // an RCE vector. Type identity belongs in the wt-event-type header, never in the body.
        var asText = Encoding.UTF8.GetString(bytes);
        asText.Should().NotContain(nameof(SerializerPayload));
        asText.Should().NotContain("WoW.Two.Sdk");
    }

    [Fact]
    public void Typeless_payload_from_a_hostile_sender_does_not_instantiate_the_type_it_names()
    {
        // What an attacker sends: a typeless-encoded payload naming a CLR type. The anchor assert below proves this
        // fixture really does carry the type name, so the negative result after it is meaningful.
        var hostile = MessagePackSerializer.Serialize<object>(
            Sample,
            MessagePackSerializerOptions.Standard.WithResolver(TypelessContractlessStandardResolver.Instance));
        Encoding.UTF8.GetString(hostile).Should().Contain(nameof(SerializerPayload));

        var deserialize = () => new MessagePackMessageSerializer().Deserialize(hostile, typeof(object));

        // The SDK's options never reach a typeless resolver, so the ext-typed header is simply not decodable —
        // the sender does not get to choose the CLR type that gets constructed.
        deserialize.Should().Throw<MessagePackSerializationException>();
    }

    // ---- empty payload: all three return null, per the interface contract ---------------------------------------

    [Fact]
    public void SystemTextJson_returns_null_on_an_empty_payload()
    {
        // Was the odd one out: it threw where the contract documents null, and TryReconstruct is called OUTSIDE the
        // adapters' try/catch — so the JsonException escaped the consume loop and killed the whole subscription
        // instead of costing one message. Null routes it to the unparseable path, which dead-letters.
        new SystemTextJsonMessageSerializer()
            .Deserialize(ReadOnlySpan<byte>.Empty, typeof(SerializerPayload))
            .Should().BeNull();
    }

    [Fact]
    public void MessagePack_returns_null_on_an_empty_payload()
    {
        new MessagePackMessageSerializer().Deserialize(ReadOnlySpan<byte>.Empty, typeof(SerializerPayload)).Should().BeNull();
    }

    [Fact]
    public void CloudEvents_returns_null_on_an_empty_payload()
    {
        CloudEvents().Deserialize(ReadOnlySpan<byte>.Empty, typeof(SerializerPayload)).Should().BeNull();
    }

    private static IMessageSerializer Resolve(string name) => name switch
    {
        "stj" => new SystemTextJsonMessageSerializer(),
        "msgpack" => new MessagePackMessageSerializer(),
        "cloudevents" => CloudEvents(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown serializer"),
    };
}
