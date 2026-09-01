using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>One message that a bulk redrive did not put back, and why.</summary>
/// <param name="MessageId">The message that was skipped.</param>
/// <param name="Outcome">Why it was skipped.</param>
/// <param name="Detail">Exception detail when <paramref name="Outcome"/> is <see cref="RedriveOutcome.Failed"/>.</param>
public sealed record DeadLetterRedriveFailure(string MessageId, RedriveOutcome Outcome, string? Detail = null);
