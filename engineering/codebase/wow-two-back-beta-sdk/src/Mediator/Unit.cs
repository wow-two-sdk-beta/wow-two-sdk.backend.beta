namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Void-equivalent type for `IRequest` (no response). Mirrors MediatR's `Unit`.</summary>
public readonly record struct Unit
{
    /// <summary>Holds the single value.</summary>
    public static readonly Unit Value;

    /// <summary>Holds completed task wrapping <see cref="Value"/>.</summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>Completed <see cref="ValueTask{TResult}"/> wrapping <see cref="Value"/>.</summary>
    public static ValueTask<Unit> ValueTask => new(Value);
}
