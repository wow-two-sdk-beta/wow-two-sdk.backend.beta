using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Default resolver: registered event types use their stable <see cref="Type.FullName"/> token; resolution tries the
/// registry, then aliases (both via <see cref="MessageTypeRegistry"/>), then falls back to <see cref="Type.GetType(string)"/>
/// / an assembly scan for assembly-qualified-name back-compat. Unregistered types emit their AQN.
/// </summary>
public sealed class MessageTypeMapper(MessageTypeRegistry registry) : IMessageTypeMapper
{
    private static readonly ConcurrentDictionary<string, Type?> FallbackCache = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string ToTypeToken(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return registry.TryGetToken(type, out var token) ? token : type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    }

    /// <inheritdoc />
    public Type? ResolveType(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return registry.TryGetType(token, out var type) ? type : FallbackCache.GetOrAdd(token, ResolveFallback);
    }

    private static Type? ResolveFallback(string token)
    {
        var type = Type.GetType(token); // assembly-qualified-name back-compat
        if (type is not null)
            return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(token); // resolve by full name across loaded assemblies
            if (type is not null)
                return type;
        }

        return null;
    }
}
