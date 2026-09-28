using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Meta;

/// <summary>Covers binding proxy trust onto <see cref="ApiDefaultsOptions"/> from a configuration section.</summary>
public sealed class ApiDefaultsOptionsExtensionsTests
{
    [Fact]
    public void TrustProxiesFrom_ShouldAddTheSectionsProxiesAndNetworks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Deployment:TrustedProxies:0"] = "10.0.0.5",
                ["Deployment:TrustedNetworks:0"] = "172.16.0.0/12",
                ["Deployment:TrustedNetworks:1"] = " ",
            })
            .Build();
        var options = new ApiDefaultsOptions();

        options.TrustProxiesFrom(configuration.GetSection("Deployment"));

        options.TrustedProxies.Should().Equal("10.0.0.5");
        options.TrustedNetworks.Should().Equal("172.16.0.0/12");
    }

    [Fact]
    public void TrustProxiesFrom_ShouldTrustNothingMore_WhenTheSectionIsMissing()
    {
        var options = new ApiDefaultsOptions();

        options.TrustProxiesFrom(new ConfigurationBuilder().Build().GetSection("Deployment"));

        options.TrustedProxies.Should().BeEmpty();
        options.TrustedNetworks.Should().BeEmpty();
    }
}
