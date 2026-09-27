namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Holds options for one batch pipeline.</summary>
public sealed record BatchPipelineOptions
{
    /// <summary>Gets or sets the name that tags this pipeline's metrics and logs. Defaults to the item type name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets how many accepted items may wait for a batch. Defaults to 10,000.</summary>
    public int Capacity { get; set; } = 10_000;

    /// <summary>Gets or sets the most items handed to one batch. Defaults to 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Gets or sets how long a partial batch waits for more items. Defaults to zero: flush what is available.</summary>
    public TimeSpan MaxBatchDelay { get; set; } = TimeSpan.Zero;
}
