using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Brokers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Geo.IpLocation;

/// <summary>The DB-IP Lite registration points the broker at the file its refresh maintains.</summary>
public sealed class IpLocationRegistrationTests
{
    [Fact]
    public void AddDbIpLiteIpLocation_PointsTheBrokerAtTheDownloadedFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "wow2-dbip-registration");
        var services = new ServiceCollection().AddLogging();
        services.AddDbIpLiteIpLocation(options => options.DatabaseDirectory = directory);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<MmdbIpLocationOptions>().DatabasePath
            .Should().Be(Path.Combine(directory, "dbip-country-lite.mmdb"));
        provider.GetRequiredService<IIpLocationBroker>().Should().BeOfType<MmdbIpLocationBroker>();
        provider.GetServices<IHostedService>().Should().ContainSingle();
    }

    [Fact]
    public void AddDbIpLiteIpLocation_RejectsAMissingDirectory()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbIpLiteIpLocation(_ => { });
        using var provider = services.BuildServiceProvider();

        var resolve = () => provider.GetRequiredService<DbIpLiteOptions>();

        resolve.Should().Throw<OptionsValidationException>();
    }
}
