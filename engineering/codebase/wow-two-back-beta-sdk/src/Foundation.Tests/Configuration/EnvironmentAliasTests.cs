using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Backend.Beta.Foundation.Configuration;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Configuration;

/// <summary>
/// Covers <see cref="EnvironmentAliasExtensions.UseEnvironmentAliases"/>: listed variables reach their mapped keys,
/// unlisted and empty ones never do, hosting keys survive and later sources still win.
/// </summary>
public sealed class EnvironmentAliasTests : IDisposable
{
    // Unique names keep parallel test classes from sharing process-wide variables.
    private readonly string _prefix = $"WOW_ALIAS_{Guid.NewGuid():N}_";

    private readonly List<string> _variables = [];

    private string Set(string suffix, string? value)
    {
        var name = _prefix + suffix;
        _variables.Add(name);
        Environment.SetEnvironmentVariable(name, value);
        return name;
    }

    public void Dispose()
    {
        foreach (var name in _variables) Environment.SetEnvironmentVariable(name, null);
    }

    private static ConfigurationManager WithEnvironment(IDictionary<string, string?>? files = null)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(files ?? new Dictionary<string, string?>());
        configuration.AddEnvironmentVariables();
        return configuration;
    }

    [Fact]
    public void UseEnvironmentAliases_ShouldMapListedVariables_AndDropUnlistedOnes()
    {
        var database = Set("DB", "Host=db");
        var stray = Set("STRAY", "value");
        var configuration = WithEnvironment(new Dictionary<string, string?> { ["Database:Connection"] = "Host=file" });

        configuration.UseEnvironmentAliases(new Dictionary<string, string> { [database] = "Database:Connection" });

        configuration["Database:Connection"].Should().Be("Host=db");
        configuration[stray].Should().BeNull();
    }

    [Fact]
    public void UseEnvironmentAliases_ShouldIgnoreEmptyVariables()
    {
        var empty = Set("EMPTY", "  ");
        var configuration = WithEnvironment(new Dictionary<string, string?> { ["Auth:ClientId"] = "from-file" });

        configuration.UseEnvironmentAliases(new Dictionary<string, string> { [empty] = "Auth:ClientId" });

        configuration["Auth:ClientId"].Should().Be("from-file");
    }

    [Fact]
    public void UseEnvironmentAliases_ShouldKeepHostingKeys_AndLetLaterSourcesWin()
    {
        var database = Set("DB2", "Host=env");
        var configuration = WithEnvironment(new Dictionary<string, string?> { ["urls"] = "http://+:8080" });

        configuration.UseEnvironmentAliases(new Dictionary<string, string> { [database] = "Db" });
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Db"] = "Host=test" });

        configuration["urls"].Should().Be("http://+:8080");
        configuration["Db"].Should().Be("Host=test");
    }
}
