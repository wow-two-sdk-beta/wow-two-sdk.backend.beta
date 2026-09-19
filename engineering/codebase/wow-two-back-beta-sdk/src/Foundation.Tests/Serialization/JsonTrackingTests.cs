using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Serialization;

public sealed class JsonTrackingTests
{
    [Fact]
    public void Snapshot_IsolatesNestedMutationAndUsesStoredRepresentation()
    {
        var comparer = new JsonValueComparer<GraphModel>(StoredJsonConstants.Default);
        var original = new GraphModel { Values = [new LeafModel { Tags = ["before"] }] };
        var snapshot = comparer.Snapshot(original);
        Assert.True(comparer.Equals(original, snapshot));
        Assert.Equal(comparer.GetHashCode(original), comparer.GetHashCode(snapshot));
        original.Values[0].Tags.Add("after");
        Assert.False(comparer.Equals(original, snapshot));
        Assert.Single(snapshot.Values[0].Tags);
        var restored = JsonSerializer.Deserialize<GraphModel>(JsonSerializer.Serialize(original, StoredJsonConstants.Default), StoredJsonConstants.Default)!;
        Assert.True(comparer.Equals(original, restored));
    }

    private sealed record GraphModel { public required List<LeafModel> Values { get; init; } }
    private sealed record LeafModel { public required List<string> Tags { get; init; } }
}
