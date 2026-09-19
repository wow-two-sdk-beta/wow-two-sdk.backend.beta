using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Security;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Security;

/// <summary>Covers the configured path of <c>AddEnvelopeCryptography</c> — the bare record resolves when a caller supplies a delegate.</summary>
public sealed class EnvelopeCryptographyRegistrationTests
{
    [Fact]
    public void AddEnvelopeCryptography_ShouldResolveTheConfiguredRecord()
    {
        using var provider = new ServiceCollection()
            .AddEnvelopeCryptography(options => options.MasterKeyEnvironmentVariable = "KEK")
            .BuildServiceProvider();

        provider.GetRequiredService<EnvelopeCryptographyOptions>().MasterKeyEnvironmentVariable.Should().Be("KEK");
        provider.GetRequiredService<IMasterKeyBroker>().Should().NotBeNull();
        provider.GetRequiredService<IValueCipher>().Should().NotBeNull();
    }
}
