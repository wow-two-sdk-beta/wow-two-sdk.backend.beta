using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Represents tally of a bulk redrive.</summary>
public sealed record DeadLetterRedriveResult
{
    /// <summary>Records the query selected.</summary>
    public required int Matched { get; init; }

    /// <summary>Records actually put back.</summary>
    public required int Redriven { get; init; }

    /// <summary>Per-message reasons for the rest; empty when everything matched was redriven.</summary>
    public required IReadOnlyList<DeadLetterRedriveFailure> Failures { get; init; }

    /// <summary>An empty result — nothing matched, nothing to do.</summary>
    public static DeadLetterRedriveResult Empty { get; } = new() { Matched = 0, Redriven = 0, Failures = [] };

    /// <summary>True when every matched record was redriven.</summary>
    public bool IsComplete => Failures.Count == 0;
}
