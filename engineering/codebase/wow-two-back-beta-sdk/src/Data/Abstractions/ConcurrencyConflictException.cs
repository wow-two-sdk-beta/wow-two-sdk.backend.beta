namespace WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

/// <summary>
/// Represents a write whose concurrency token no longer matches the stored row — another writer changed or removed it
/// since it was read. Raised by repositories outside EF Core, which raises its own <c>DbUpdateConcurrencyException</c>;
/// both map to a conflict.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates the exception for the entity <paramref name="entityType"/> keyed <paramref name="id"/>.</summary>
    /// <param name="entityType">The entity type written.</param>
    /// <param name="id">The key of the row written.</param>
    public ConcurrencyConflictException(Type entityType, object? id)
        : base($"{entityType?.Name} '{id}' was changed or removed by another writer since it was read.")
    {
        ArgumentNullException.ThrowIfNull(entityType);
        EntityType = entityType;
        Id = id;
    }

    /// <summary>Creates the exception with a default message.</summary>
    public ConcurrencyConflictException()
        : base("The row was changed or removed by another writer since it was read.")
    {
        EntityType = typeof(object);
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
        EntityType = typeof(object);
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
        EntityType = typeof(object);
    }

    /// <summary>The entity type written.</summary>
    public Type EntityType { get; }

    /// <summary>The key of the row written, when known.</summary>
    public object? Id { get; }
}
