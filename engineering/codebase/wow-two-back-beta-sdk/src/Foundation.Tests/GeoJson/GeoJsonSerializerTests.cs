using System.Text.Json;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Geo.GeoJson.Serializers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.GeoJson;

/// <summary>Covers the supported GeoJSON subset and the boundaries that prevent silent shape loss.</summary>
public sealed class GeoJsonSerializerTests
{
    private readonly GeoJsonSerializer _serializer = new();

    [Fact]
    public void Feature_ShouldPreserveNumericIdKind()
    {
        const string json = """{"type":"Feature","id":42,"geometry":null,"properties":null}""";

        var feature = _serializer.ParseFeature(json);
        var serialized = _serializer.Serialize(feature);
        using var document = JsonDocument.Parse(serialized);

        feature.Id.Should().NotBeNull();
        feature.Id!.Value.ValueKind.Should().Be(JsonValueKind.Number);
        document.RootElement.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Theory]
    [InlineData("""{"type":"FeatureCollection"}""")]
    [InlineData("""{"type":"FeatureCollection","features":null}""")]
    [InlineData("""{"type":"Feature","features":[]}""")]
    public void FeatureCollection_ShouldRejectWrongOrMissingShape(string json)
    {
        var act = () => _serializer.ParseFeatureCollection(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Feature_ShouldRejectWrongDiscriminator()
    {
        var act = () => _serializer.ParseFeature("""{"type":"Other","geometry":null,"properties":null}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Geometry_ShouldRejectUnsupportedPositionElements()
    {
        var act = () => _serializer.ParseGeometry("""{"type":"Point","coordinates":[1,2,3,4]}""");

        act.Should().Throw<JsonException>();
    }
}
