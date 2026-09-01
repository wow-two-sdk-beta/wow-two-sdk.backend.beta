using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Registration-time map of event types ↔ their stable wire tokens (+ aliases for post-rename compatibility).
/// Populated by the handler scan (<c>AddEventHandlersFromAssemblies</c>) and consulted by <see cref="MessageTypeMapper"/>.
/// </summary>
public sealed class MessageTypeRegistry
{
    private readonly ConcurrentDictionary<Type, string> _tokenByType = new();
    private readonly ConcurrentDictionary<string, Type> _typeByToken = new(StringComparer.Ordinal);

    /// <summary>Register <paramref name="type"/> under its token (default = the type's full name). Idempotent.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="token">Explicit token; defaults to <see cref="Type.FullName"/>.</param>
    public void Register(Type type, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        var resolved = token ?? type.FullName ?? type.Name;
        _tokenByType[type] = resolved;
        _typeByToken[resolved] = type;
    }

    /// <summary>Map an alias token (e.g. an old contract name after a rename) to an existing type.</summary>
    /// <param name="alias">The alias token.</param>
    /// <param name="type">The current type.</param>
    public void AddAlias(string alias, Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);
        ArgumentNullException.ThrowIfNull(type);
        _typeByToken[alias] = type;
    }

    /// <summary>Try to get the registered token for a type.</summary>
    /// <param name="type">The type.</param>
    /// <param name="token">The token, when found.</param>
    public bool TryGetToken(Type type, [MaybeNullWhen(false)] out string token) => _tokenByType.TryGetValue(type, out token);

    /// <summary>Try to resolve a token to a registered type.</summary>
    /// <param name="token">The token.</param>
    /// <param name="type">The type, when found.</param>
    public bool TryGetType(string token, [MaybeNullWhen(false)] out Type type) => _typeByToken.TryGetValue(token, out type);
}
