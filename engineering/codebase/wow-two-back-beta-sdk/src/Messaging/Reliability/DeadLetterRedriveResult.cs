using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Tally of a bulk redrive.</summary>
/// <param name="Matched">Records the query selected.</param>
/// <param name="Redriven">Records actually put back.</param>
/// <param name="Failures">Per-message reasons for the rest; empty when everything matched was redriven.</param>
public sealed record DeadLetterRedriveResult(int Matched, int Redriven, IReadOnlyList<DeadLetterRedriveFailure> Failures)
{
    /// <summary>An empty result — nothing matched, nothing to do.</summary>
    public static DeadLetterRedriveResult Empty { get; } = new(0, 0, []);

    /// <summary>True when every matched record was redriven.</summary>
    public bool IsComplete => Failures.Count == 0;
}
