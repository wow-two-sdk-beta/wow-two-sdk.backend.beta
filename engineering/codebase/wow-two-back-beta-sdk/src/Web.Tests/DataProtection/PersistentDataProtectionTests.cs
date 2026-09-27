using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Web.DataProtection;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.DataProtection;

/// <summary>A persistent key ring lets a replacement host read payloads protected before it started.</summary>
public sealed class PersistentDataProtectionTests : IDisposable
{
    private readonly string _keys = Path.Combine(Path.GetTempPath(), "wow2-keys", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ProtectedPayload_SurvivesAHostReplacement()
    {
        string token;
        using (var first = Build(Environments.Production, _keys))
        {
            token = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("guest").Protect("owner-1");
        }

        using var replacement = Build(Environments.Production, _keys);

        replacement.GetRequiredService<IDataProtectionProvider>().CreateProtector("guest").Unprotect(token).Should().Be("owner-1");
        Directory.GetFiles(_keys, "*.xml").Should().NotBeEmpty();
    }

    [Fact]
    public void KeyDirectory_IsRequiredOutsideDevelopment()
    {
        using var production = Build(Environments.Production, keyDirectory: null);
        using var development = Build(Environments.Development, keyDirectory: null);

        var resolveProduction = () => production.GetRequiredService<PersistentDataProtectionOptions>();

        resolveProduction.Should().Throw<OptionsValidationException>();
        development.GetRequiredService<PersistentDataProtectionOptions>().KeyDirectory.Should().BeNull();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_keys, recursive: true);
        }
        catch (IOException)
        {
            // A missing or leaked temp directory must not fail a test.
        }
    }

    private static ServiceProvider Build(string environment, string? keyDirectory)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(environment));
        services.AddPersistentDataProtection(options =>
        {
            options.ApplicationName = "WoW2.Tests";
            options.KeyDirectory = keyDirectory;
        });
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "WoW2.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
