using AwesomeAssertions;
using CloneApi = FastCloner.FastCloner;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Copying;

public sealed class FastClonerContractTests
{
    private sealed record Graph
    {
        private readonly List<Node> _privateNodes = [];

        public Node First { get; init; } = new();
        public Node Second { get; init; } = new();
        public List<Node> Nodes { get; init; } = [];
        public List<Node> ReadOnlyNodes { get; } = [];
        public BaseData Derived { get; init; } = new();
        public Graph? Next { get; set; }

        public void AddPrivate(Node node) => _privateNodes.Add(node);
        public Node GetPrivate() => _privateNodes.Single();
    }

    private sealed class Node
    {
        public string Name { get; set; } = string.Empty;
        public Node? Next { get; set; }
    }

    private class BaseData;

    private sealed class DerivedData : BaseData
    {
        public List<string> Details { get; init; } = [];
    }

    private sealed record CompositeKey(int Id, List<string> Parts);

    private sealed record KeyGraph
    {
        public required CompositeKey Key { get; init; }
        public HashSet<CompositeKey> Set { get; init; } = [];
        public Dictionary<CompositeKey, string> Map { get; init; } = [];
    }

    private sealed record IdentityGraph
    {
        public required Node Node { get; init; }
        public HashSet<Node> Set { get; init; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<Node, string> Map { get; init; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<string, Node> CaseMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record CandidateEntity
    {
        public required Guid Id { get; init; }
        public List<string> Values { get; init; } = [];
    }

    [Fact]
    public void DeepClone_ShouldPreserveRuntimeTypesCyclesAliasesAndHiddenState()
    {
        var shared = new Node { Name = "shared" };
        shared.Next = shared;
        var original = new Graph
        {
            First = shared,
            Second = shared,
            Derived = new DerivedData { Details = ["derived"] },
        };
        original.Next = original;
        original.Nodes.Add(shared);
        original.ReadOnlyNodes.Add(shared);
        original.AddPrivate(shared);

        var copy = CloneApi.DeepClone(original)!;

        copy.Should().NotBeSameAs(original);
        copy.Derived.Should().BeOfType<DerivedData>();
        copy.Next.Should().BeSameAs(copy);
        copy.First.Next.Should().BeSameAs(copy.First);
        copy.Second.Should().BeSameAs(copy.First);
        copy.Nodes.Single().Should().BeSameAs(copy.First);
        copy.ReadOnlyNodes.Single().Should().BeSameAs(copy.First);
        copy.GetPrivate().Should().BeSameAs(copy.First);
        copy.ReadOnlyNodes.Should().NotBeSameAs(original.ReadOnlyNodes);
        copy.GetPrivate().Should().NotBeSameAs(original.GetPrivate());
    }

    [Fact]
    public void DeepClone_ShouldPreserveCollectionComparersAndIdentityKeys()
    {
        var node = new Node { Name = "key" };
        var original = new IdentityGraph { Node = node };
        original.Set.Add(node);
        original.Map.Add(node, "value");
        original.CaseMap.Add("HELLO", node);

        var copy = CloneApi.DeepClone(original)!;

        copy.Set.Contains(copy.Node).Should().BeTrue();
        copy.Set.Contains(original.Node).Should().BeFalse();
        copy.Map[copy.Node].Should().Be("value");
        copy.Map.ContainsKey(original.Node).Should().BeFalse();
        copy.CaseMap.ContainsKey("hello").Should().BeTrue();
    }

    [Fact]
    public void DeepClone_ShouldRebuildHashesForRecordKeysWithReferenceMembers()
    {
        var key = new CompositeKey(1, ["part"]);
        var original = new KeyGraph { Key = key };
        original.Set.Add(key);
        original.Map.Add(key, "value");

        var copy = CloneApi.DeepClone(original)!;

        copy.Key.Should().BeSameAs(copy.Set.Single());
        copy.Key.Should().BeSameAs(copy.Map.Keys.Single());
        copy.Key.Parts.Should().NotBeSameAs(original.Key.Parts);
        copy.Set.Contains(copy.Key).Should().BeTrue();
        copy.Map[copy.Key].Should().Be("value");
    }

    [Fact]
    public void DeepClone_ShouldIsolateMutableStateAndPreserveEntityIdentity()
    {
        var id = Guid.NewGuid();
        var original = new CandidateEntity { Id = id, Values = ["original"] };

        var copy = CloneApi.DeepClone(original)!;
        copy.Values.Add("copy-only");

        copy.Id.Should().Be(id);
        copy.Values.Should().Equal("original", "copy-only");
        original.Values.Should().Equal("original");
    }
}
