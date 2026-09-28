using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>
/// Creates System.Text.Json cache serializers whose read failures surface as <see cref="CacheDeserializationException"/>,
/// so <see cref="HybridCacheRepository"/> can apply <see cref="HybridCacheConventionOptions.DeserializationFailure"/>.
/// Strings and byte arrays keep HybridCache's built-in serializers.
/// </summary>
public sealed class JsonCacheSerializerFactory : IHybridCacheSerializerFactory
{
    /// <inheritdoc />
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        serializer = typeof(T) == typeof(string) || typeof(T) == typeof(byte[]) ? null : new JsonCacheSerializer<T>();
        return serializer is not null;
    }

    /// <summary>Serializes cache values as UTF-8 JSON with the default options.</summary>
    /// <typeparam name="T">The cached type.</typeparam>
    private sealed class JsonCacheSerializer<T> : IHybridCacheSerializer<T>
    {
        public T Deserialize(ReadOnlySequence<byte> source)
        {
            var reader = new Utf8JsonReader(source);
            try
            {
                return JsonSerializer.Deserialize<T>(ref reader, JsonSerializerOptions.Default)!;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                throw new CacheDeserializationException(null, typeof(T), exception);
            }
        }

        public void Serialize(T value, IBufferWriter<byte> target)
        {
            using var writer = new Utf8JsonWriter(target);
            JsonSerializer.Serialize(writer, value, JsonSerializerOptions.Default);
        }
    }
}
