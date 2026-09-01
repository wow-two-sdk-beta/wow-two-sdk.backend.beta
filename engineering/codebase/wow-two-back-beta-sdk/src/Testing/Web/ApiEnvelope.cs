using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>
/// Test-side mirror of the SDK success envelope (<c>ApiResponse&lt;T&gt;.Success</c>) carrying the payload under <c>data</c>.
/// </summary>
/// <remarks>Only the <c>data</c> field is read — the rest of the envelope is dropped.</remarks>
/// <typeparam name="T">The wrapped payload type.</typeparam>
public sealed record ApiEnvelope<T>
{
    /// <summary>Gets the wrapped payload, deserialized from <c>.data</c>.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }
}
