using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.EventSaga;

/// <summary>Shared, correlated state threaded through every step of an event-saga run.</summary>
public sealed class EventSagaContext
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    /// <summary>Create a context with an optional seed value and a fresh correlation id.</summary>
    /// <param name="seed">Optional initial input for the first step.</param>
    public EventSagaContext(object? seed = null)
    {
        Seed = seed;
        CorrelationId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Correlation id shared by every event the saga emits.</summary>
    public string CorrelationId { get; }

    /// <summary>The seed value supplied at construction.</summary>
    public object? Seed { get; }

    /// <summary>The accumulated step outputs.</summary>
    public IReadOnlyDictionary<string, object?> Items => _items;

    /// <summary>Store a value for downstream steps.</summary>
    /// <param name="key">Item key.</param>
    /// <param name="value">Item value.</param>
    public void Set(string key, object? value) => _items[key] = value;

    /// <summary>Read a typed value written by an earlier step, or <c>default</c> if absent / mistyped.</summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="key">Item key.</param>
    public T? Get<T>(string key) => _items.TryGetValue(key, out var value) && value is T typed ? typed : default;
}
