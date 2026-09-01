using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>Options for the outbox dispatcher hosted service.</summary>
public sealed record OutboxDispatcherOptions
{
    /// <summary>How often the dispatcher polls for pending rows. Default 5s.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum rows dispatched per pass. Default 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Attempts before a poison row is given up on (stamped processed, error retained) so it stops re-selecting forever. Default 10.</summary>
    public int MaxDispatchAttempts { get; set; } = 10;

    /// <summary>How long processed rows are retained before pruning. Default 7 days.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the dispatcher prunes old processed rows. Default 15 minutes.</summary>
    public TimeSpan PruneInterval { get; set; } = TimeSpan.FromMinutes(15);
}
