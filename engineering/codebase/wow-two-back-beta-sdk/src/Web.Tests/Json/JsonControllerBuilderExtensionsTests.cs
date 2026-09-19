using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Web.Json;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Json;

public sealed class JsonControllerBuilderExtensionsTests
{
    private enum Status
    {
        Pending,
        InProgress,
    }

    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    private sealed record WireContract
    {
        public required Status Status { get; init; }
        public required Access Access { get; init; }
        public required Dictionary<string, int> CountsByKind { get; init; }
        public required Guid Id { get; init; }
        public required bool Enabled { get; init; }
        public required decimal Amount { get; init; }
        public required int Count { get; init; }
        public required long Total { get; init; }
        public required DateTimeOffset OccurredAt { get; init; }
        public required DateOnly Day { get; init; }
        public required TimeOnly Time { get; init; }
        public required TimeSpan Duration { get; init; }
        public string? Note { get; init; }
    }

    [Fact]
    public void AddControllersWithSdkJson_ShouldApplyTheCompleteWireContract()
    {
        var options = ResolveOptions(services => services.AddControllersWithSdkJson());
        var expected = new WireContract
        {
            Status = Status.InProgress,
            Access = Access.Read | Access.Write,
            CountsByKind = new Dictionary<string, int> { ["SomeKind"] = 3 },
            Id = Guid.Parse("ba6f6e63-10c7-49b5-bd6b-fffdc8fc3b8b"),
            Enabled = true,
            Amount = 12.5m,
            Count = 4,
            Total = 9_000_000_000,
            OccurredAt = new DateTimeOffset(2026, 9, 16, 12, 34, 56, TimeSpan.FromHours(5)),
            Day = new DateOnly(2026, 9, 16),
            Time = new TimeOnly(12, 34, 56),
            Duration = TimeSpan.FromDays(2) + TimeSpan.FromSeconds(1),
            Note = null,
        };

        var json = JsonSerializer.Serialize(expected, options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().Be("inProgress");
        root.GetProperty("access").GetString().Should().Be("read, write");
        root.GetProperty("countsByKind").TryGetProperty("someKind", out _).Should().BeTrue();
        root.GetProperty("duration").GetString().Should().Be("P2DT1S");
        root.TryGetProperty("note", out _).Should().BeFalse();
        var restored = JsonSerializer.Deserialize<WireContract>(json, options);
        restored.Should().BeEquivalentTo(expected, comparison => comparison.Excluding(contract => contract.CountsByKind));
        restored!.CountsByKind.Should().ContainSingle("someKind", 3);
    }

    [Theory]
    [InlineData("{\"status\":99}")]
    [InlineData("{\"status\":1}")]
    public void AddControllersWithSdkJson_ShouldRejectNumericEnumsOnRead(string json)
    {
        var options = ResolveOptions(services => services.AddControllersWithSdkJson());

        var act = () => JsonSerializer.Deserialize<WireContract>(json, options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void AddControllersWithSdkJson_ShouldRejectUndefinedEnumsAndFlagsOnWrite()
    {
        var options = ResolveOptions(services => services.AddControllersWithSdkJson());

        var undefinedEnum = () => JsonSerializer.Serialize((Status)99, options);
        var undefinedFlags = () => JsonSerializer.Serialize((Access)8, options);

        undefinedEnum.Should().Throw<JsonException>();
        undefinedFlags.Should().Throw<JsonException>();
    }

    [Fact]
    public void AddJsonStringEnums_ShouldRejectNumericEnumsInBothDirections()
    {
        var options = ResolveOptions(services => services.AddControllers().AddJsonStringEnums());

        var read = () => JsonSerializer.Deserialize<Status>("1", options);
        var write = () => JsonSerializer.Serialize((Status)99, options);

        read.Should().Throw<JsonException>();
        write.Should().Throw<JsonException>();
    }

    private static JsonSerializerOptions ResolveOptions(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value.JsonSerializerOptions;
    }
}
