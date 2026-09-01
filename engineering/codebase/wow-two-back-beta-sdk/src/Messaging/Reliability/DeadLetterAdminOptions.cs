using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Options for <see cref="IDeadLetterAdmin"/>.</summary>
public sealed record DeadLetterAdminOptions
{
    /// <summary>
    /// How many times one message may be redriven before the guard refuses it. Default 3; zero or negative disables
    /// redrive entirely, which is a way to run the admin read-only.
    /// </summary>
    /// <remarks>
    ///   - a redrive returns the message to the handler that already rejected it
    ///   - over an unfixed handler, a bulk redrive re-fills the store as fast as it drains
    /// </remarks>
    public int MaxRedrives { get; set; } = 3;

    /// <summary>
    /// Quarantine a record when a redrive is refused for hitting <see cref="MaxRedrives"/>, so the next bulk redrive
    /// skips it instead of re-testing and re-failing it. Default <c>true</c>.
    /// </summary>
    public bool QuarantineAtRedriveLimit { get; set; } = true;
}
