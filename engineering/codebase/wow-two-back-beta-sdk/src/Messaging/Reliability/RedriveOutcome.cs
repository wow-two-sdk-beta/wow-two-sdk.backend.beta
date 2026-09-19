using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Refers to what happened to a single redrive request.</summary>
public enum RedriveOutcome
{
    /// <summary>Re-published to its original destination with a fresh retry budget.</summary>
    Redriven,

    /// <summary>No record with that message id is in the store.</summary>
    NotFound,

    /// <summary>Already redriven <see cref="DeadLetterAdminOptions.MaxRedrives"/> times — the infinite-redrive guard refused it.</summary>
    LimitReached,

    /// <summary>Quarantined; release it before redriving.</summary>
    Quarantined,

    /// <summary>The store rejected the replay. The record is left in place, so the request can be repeated.</summary>
    Failed,

    /// <summary>The registered store cannot service the request — a by-id operation on a store that is not an <see cref="IDeadLetterQueryRepository"/>.</summary>
    NotSupported,
}
