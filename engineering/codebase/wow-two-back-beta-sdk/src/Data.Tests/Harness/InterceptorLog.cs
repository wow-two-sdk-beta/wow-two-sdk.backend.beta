using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The shared ordered log every recording interceptor appends to — invocation order is the assertion for the ordering cases.</summary>
public sealed class InterceptorLog
{
    private readonly ConcurrentQueue<string> _entries = new();

    /// <summary>Every recorded entry, in invocation order.</summary>
    public IReadOnlyList<string> Entries => [.. _entries];

    /// <summary>Records one invocation.</summary>
    /// <param name="entry">The entry to append.</param>
    public void Add(string entry) => _entries.Enqueue(entry);

    /// <summary>How many entries carry the given value.</summary>
    /// <param name="entry">The entry to count.</param>
    public int CountOf(string entry) => _entries.Count(existing => existing == entry);
}
