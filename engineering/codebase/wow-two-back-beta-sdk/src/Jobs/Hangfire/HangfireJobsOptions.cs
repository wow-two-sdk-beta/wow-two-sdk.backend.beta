namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

/// <summary>Holds server tuning for Hangfire background processing.</summary>
public sealed record HangfireJobsOptions
{
    /// <summary>Concurrent workers. Null = Hangfire default (processor count × 5).</summary>
    public int? WorkerCount { get; set; }

    /// <summary>Queues this server processes, in priority order. Empty = the <c>default</c> queue.</summary>
    public IList<string> Queues { get; } = [];

    /// <summary>
    /// How often scheduled and retried jobs move onto their queue. Null = Hangfire default (15 seconds), which also
    /// bounds how late a short retry delay may start.
    /// </summary>
    public TimeSpan? SchedulePollingInterval { get; set; }
}
