using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Errors;

public sealed class ExceptionChainTests
{
    [Fact]
    public void Flatten_ShouldReturnOneEntry_WhenSingleException()
    {
        var exception = new InvalidOperationException("boom");

        var entries = ExceptionChain.Flatten(exception);

        entries.Should().ContainSingle()
            .Which.Should().Be("InvalidOperationException: boom");
    }

    [Fact]
    public void Flatten_ShouldWalkInnerChainInOrder()
    {
        var root = new InvalidOperationException("outer", new ArgumentException("inner"));

        var entries = ExceptionChain.Flatten(root);

        entries.Should().HaveCount(2);
        entries[0].Should().StartWith("InvalidOperationException: outer");
        entries[1].Should().StartWith("ArgumentException: inner");
    }

    [Fact]
    public void Flatten_ShouldCapDepthAtFive()
    {
        Exception current = new InvalidOperationException("level-0");
        for (var i = 1; i < 10; i++)
        {
            current = new InvalidOperationException($"level-{i}", current);
        }

        var entries = ExceptionChain.Flatten(current);

        entries.Should().HaveCount(5);
    }

    [Fact]
    public void Flatten_ShouldExpandAggregateExceptionInners()
    {
        var aggregate = new AggregateException(
            new InvalidOperationException("first"),
            new ArgumentException("second"));

        var entries = ExceptionChain.Flatten(aggregate);

        entries.Should().HaveCount(2);
        entries.Should().Contain(entry => entry.Contains("first", StringComparison.Ordinal));
        entries.Should().Contain(entry => entry.Contains("second", StringComparison.Ordinal));
    }

    [Fact]
    public void Flatten_ShouldThrow_WhenNull()
    {
        var act = () => ExceptionChain.Flatten(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
