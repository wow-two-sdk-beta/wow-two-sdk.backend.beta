namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Holds one commit callback and its optional deduplication key.</summary>
internal sealed record DataSessionHook
{
    internal required Func<CancellationToken, ValueTask> Action { get; init; }
    internal string? Key { get; init; }
}
