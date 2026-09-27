using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Brokers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Geo.IpLocation;

/// <summary>The MMDB broker resolves public addresses, ignores private ones and hot-reloads a replaced file.</summary>
public sealed class MmdbIpLocationBrokerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wow2-ip-location", Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));

    public MmdbIpLocationBrokerTests() => Directory.CreateDirectory(_directory);

    private string DatabasePath => Path.Combine(_directory, "test.mmdb");

    [Fact]
    public void Locate_ResolvesACountryRecord()
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("AU", "Australia", "OC"))]);
        using var broker = CreateBroker();

        var location = broker.Locate(IPAddress.Parse("1.2.3.4"));

        location.Should().NotBeNull();
        location!.CountryCode.Should().Be("AU");
        location.CountryName.Should().Be("Australia");
        location.ContinentCode.Should().Be("OC");
        location.City.Should().BeNull();
        location.Coordinate.Should().BeNull();
    }

    [Fact]
    public void Locate_ResolvesACityRecord()
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("8.8.8.0/24", IpLocationTestData.City())]);
        using var broker = CreateBroker();

        var location = broker.Locate(IPAddress.Parse("8.8.8.8"));

        location!.CountryCode.Should().Be("US");
        location.Subdivision.Should().Be("California");
        location.City.Should().Be("Mountain View");
        location.Coordinate!.Latitude.Should().Be(37.386);
        location.Coordinate.Longitude.Should().Be(-122.0838);
    }

    [Theory]
    [InlineData("9.9.9.9")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void Locate_ReturnsNull_ForUnknownPrivateAndUnsupportedAddresses(string address)
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("AU", "Australia", "OC"))]);
        using var broker = CreateBroker();

        broker.Locate(IPAddress.Parse(address)).Should().BeNull();
    }

    [Fact]
    public void Locate_ResolvesAnIpv4MappedIpv6Address()
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("AU", "Australia", "OC"))]);
        using var broker = CreateBroker();

        broker.Locate(IPAddress.Parse("::ffff:1.2.3.4"))!.CountryCode.Should().Be("AU");
    }

    [Fact]
    public void Locate_ReturnsNull_WhenTheDatabaseIsMissing()
    {
        using var broker = CreateBroker();

        broker.Locate(IPAddress.Parse("1.2.3.4")).Should().BeNull();
    }

    [Fact]
    public void Locate_ReloadsAReplacedFile_AfterTheCheckInterval()
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("AU", "Australia", "OC"))]);
        using var broker = CreateBroker();
        broker.Locate(IPAddress.Parse("1.2.3.4"))!.CountryCode.Should().Be("AU");

        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("NZ", "New Zealand", "OC"))]);
        File.SetLastWriteTimeUtc(DatabasePath, DateTime.UtcNow.AddMinutes(5));
        broker.Locate(IPAddress.Parse("1.2.3.4"))!.CountryCode.Should().Be("AU", "the check interval has not elapsed");

        _clock.Advance(TimeSpan.FromMinutes(1));
        broker.Locate(IPAddress.Parse("1.2.3.4"))!.CountryCode.Should().Be("NZ");
    }

    [Fact]
    public void Locate_KeepsTheLastGoodDatabase_WhenTheReplacementIsCorrupt()
    {
        MmdbTestDatabaseWriter.Write(DatabasePath, [("1.2.3.0/24", IpLocationTestData.Country("AU", "Australia", "OC"))]);
        using var broker = CreateBroker();
        broker.Locate(IPAddress.Parse("1.2.3.4"))!.CountryCode.Should().Be("AU");

        File.WriteAllBytes(DatabasePath, [1, 2, 3, 4, 5]);
        File.SetLastWriteTimeUtc(DatabasePath, DateTime.UtcNow.AddMinutes(5));
        _clock.Advance(TimeSpan.FromMinutes(1));

        broker.Locate(IPAddress.Parse("1.2.3.4"))!.CountryCode.Should().Be("AU");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory must not fail a test.
        }
    }

    private MmdbIpLocationBroker CreateBroker() => new(
        new MmdbIpLocationOptions { DatabasePath = DatabasePath, ReloadCheckInterval = TimeSpan.FromMinutes(1) },
        _clock,
        NullLogger<MmdbIpLocationBroker>.Instance);
}
