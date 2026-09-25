namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Holds limits for transaction nesting and callback cleanup.</summary>
public sealed record DataSessionOptions
{
    /// <summary>Gets or sets the maximum simultaneous unit depth, including the root. Defaults to sixteen.</summary>
    public int MaxDepth { get; set; } = 16;

    /// <summary>Gets or sets each callback's timeout. Defaults to five seconds; later callbacks receive fresh budgets.</summary>
    public TimeSpan HookTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the rollback cleanup budget independent of request cancellation.</summary>
    public TimeSpan RollbackTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
