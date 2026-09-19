namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>
/// Maps registered event types to stable wire tokens and resolves registered tokens and aliases.
/// </summary>
public sealed class MessageTypeMapper(MessageTypeRegistry registry) : IMessageTypeMapper
{
    /// <inheritdoc />
    public string ToTypeToken(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return registry.GetToken(type);
    }

    /// <inheritdoc />
    public Type? ResolveType(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return registry.TryGetType(token, out var type) ? type : null;
    }
}
