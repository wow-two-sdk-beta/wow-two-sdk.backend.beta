using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>One message that a bulk redrive did not put back, and why.</summary>
public sealed record DeadLetterRedriveFailure
{
    /// <summary>The message that was skipped.</summary>
    public required string MessageId { get; init; }

    /// <summary>Why it was skipped.</summary>
    public required RedriveOutcome Outcome { get; init; }

    /// <summary>Exception detail when <see cref="Outcome"/> is <see cref="RedriveOutcome.Failed"/>.</summary>
    public string? Detail { get; init; }
}
