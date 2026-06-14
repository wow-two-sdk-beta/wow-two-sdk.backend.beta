namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

/// <summary>Server tuning for Hangfire background processing.</summary>
public sealed class HangfireJobsOptions
{
    /// <summary>Concurrent workers. Null = Hangfire default (processor count × 5).</summary>
    public int? WorkerCount { get; set; }

    /// <summary>Queues this server processes, in priority order. Empty = the <c>default</c> queue.</summary>
    public IList<string> Queues { get; } = [];
}
