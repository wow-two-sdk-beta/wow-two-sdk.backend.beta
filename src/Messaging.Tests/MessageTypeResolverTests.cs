using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>The type-resolver seam that replaces brittle assembly-qualified-name resolution: stable token round-trip, AQN fallback, null on unknown, and rename aliases.</summary>
public sealed class MessageTypeResolverTests
{
    [Fact]
    public void Registered_type_round_trips_via_stable_full_name_token()
    {
        var registry = new MessageTypeRegistry();
        registry.Register(typeof(PingEvent));
        var resolver = new DefaultMessageTypeResolver(registry);

        var token = resolver.ToTypeToken(typeof(PingEvent));

        token.Should().Be(typeof(PingEvent).FullName); // stable — no assembly/version, survives cross-service + rename-of-assembly
        resolver.ResolveType(token).Should().Be<PingEvent>();
    }

    [Fact]
    public void Falls_back_to_assembly_qualified_name_for_unregistered_type()
    {
        var resolver = new DefaultMessageTypeResolver(new MessageTypeRegistry());

        var token = resolver.ToTypeToken(typeof(PingEvent));

        token.Should().Be(typeof(PingEvent).AssemblyQualifiedName); // unregistered → AQN (back-compat, no regression)
        resolver.ResolveType(token).Should().Be<PingEvent>();
    }

    [Fact]
    public void Returns_null_for_unknown_token()
    {
        var resolver = new DefaultMessageTypeResolver(new MessageTypeRegistry());

        resolver.ResolveType("Nonexistent.Contract.Type, Nowhere").Should().BeNull(); // caller dead-letters, does not drop
    }

    [Fact]
    public void Alias_resolves_to_the_current_type()
    {
        var registry = new MessageTypeRegistry();
        registry.Register(typeof(PingEvent));
        registry.AddAlias("legacy.Renamed", typeof(PingEvent));
        var resolver = new DefaultMessageTypeResolver(registry);

        resolver.ResolveType("legacy.Renamed").Should().Be<PingEvent>();
    }
}
