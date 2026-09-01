using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Marker — request opts in to idempotency dedup.</summary>
public interface IIdempotent
{
    /// <summary>Stable key derived from the request (e.g. an `Idempotency-Key` header).</summary>
    string IdempotencyKey { get; }
}
