namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Provides a flattened, depth-capped view of an exception's inner chain for diagnostics.</summary>
public static class ExceptionChain
{
    private const int MaxDepth = 5;

    /// <summary>Flattens <paramref name="exception"/> and its inner chain into "{Type}: {Message}" entries.</summary>
    /// <param name="exception">The exception to flatten.</param>
    public static IReadOnlyList<string> Flatten(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var entries = new List<string>();
        var current = exception;

        while (current is not null && entries.Count < MaxDepth)
        {
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    entries.Add($"{inner.GetType().Name}: {inner.Message}");
                }

                break;
            }

            entries.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return entries;
    }
}
