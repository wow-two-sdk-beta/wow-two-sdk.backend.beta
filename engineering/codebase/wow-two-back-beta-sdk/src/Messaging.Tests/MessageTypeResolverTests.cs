using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Tests stable token round trips, required registration, unknown inbound tokens and rename aliases.</summary>
public sealed class MessageTypeResolverTests
{
    [Fact]
    public void Registered_type_round_trips_via_stable_full_name_token()
    {
        var registry = new MessageTypeRegistry();
        registry.Register(typeof(PingEvent));
        var resolver = new MessageTypeMapper(registry);

        var token = resolver.ToTypeToken(typeof(PingEvent));

        token.Should().Be(typeof(PingEvent).FullName); // stable — no assembly/version, survives cross-service + rename-of-assembly
        resolver.ResolveType(token).Should().Be<PingEvent>();
    }

    [Fact]
    public void Unregistered_outgoing_type_fails_as_incomplete_wiring()
    {
        var resolver = new MessageTypeMapper(new MessageTypeRegistry());

        var act = () => resolver.ToTypeToken(typeof(PingEvent));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MapMessageType*");
    }

    [Fact]
    public void Returns_null_for_unknown_token()
    {
        var resolver = new MessageTypeMapper(new MessageTypeRegistry());

        resolver.ResolveType("Nonexistent.Contract.Type, Nowhere").Should().BeNull(); // caller dead-letters, does not drop
    }

    [Fact]
    public void Alias_resolves_to_the_current_type()
    {
        var registry = new MessageTypeRegistry();
        registry.Register(typeof(PingEvent));
        registry.AddAlias("legacy.Renamed", typeof(PingEvent));
        var resolver = new MessageTypeMapper(registry);

        resolver.ResolveType("legacy.Renamed").Should().Be<PingEvent>();
    }
}
