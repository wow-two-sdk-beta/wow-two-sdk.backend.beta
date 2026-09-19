using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Defines behavior that maps an event CLR type to/from a stable wire token, decoupled from the assembly-qualified name (which breaks
/// across services and on any rename/assembly-move — the AQN then fails to resolve and the message is lost). Pluggable
/// via <c>AddMessageTypeMapper</c>.
/// </summary>
public interface IMessageTypeMapper
{
    /// <summary>The stable wire token for <paramref name="type"/> (survives assembly rename/version changes).</summary>
    /// <param name="type">The event type.</param>
    string ToTypeToken(Type type);

    /// <summary>Resolve a wire token back to a CLR type, or null when unknown (caller should dead-letter, not drop).</summary>
    /// <param name="token">The wire token.</param>
    Type? ResolveType(string token);
}
