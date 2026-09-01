using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// A saga write lost an optimistic-concurrency race: the stored instance changed between the load and the write, or a
/// second initiating event tried to create an instance that already exists.
/// </summary>
public sealed class SagaConcurrencyException : Exception
{
    /// <summary>Create the exception.</summary>
    public SagaConcurrencyException()
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The failure message.</param>
    public SagaConcurrencyException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying store failure, if any.</param>
    public SagaConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The correlation id of the instance that lost the race, when known.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The version the writer expected to find stored.</summary>
    public int ExpectedVersion { get; init; }

    /// <summary>Build an exception for a rejected write.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="expectedVersion">The version the writer expected to find stored.</param>
    public static SagaConcurrencyException For(string correlationId, int expectedVersion)
        => new(string.Create(CultureInfo.InvariantCulture, $"Saga instance '{correlationId}' changed concurrently; expected stored version {expectedVersion}."))
        {
            CorrelationId = correlationId,
            ExpectedVersion = expectedVersion,
        };
}
