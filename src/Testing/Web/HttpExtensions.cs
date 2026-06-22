using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Web;

/// <summary>
/// Compact JSON request/response helpers for E2E HTTP tests. All serialization uses <see cref="TestJson.Options"/>
/// so requests and responses follow the same wire contract as the SDK API serializer.
/// </summary>
public static class HttpExtensions
{
    /// <summary>Serializes <paramref name="body"/> as a UTF-8 <c>application/json</c> payload using <see cref="TestJson.Options"/>.</summary>
    /// <param name="body">The object to serialize.</param>
    public static StringContent AsJson(this object body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var json = JsonSerializer.Serialize(body, TestJson.Options);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>Sends a POST with <paramref name="body"/> serialized via <see cref="TestJson.Options"/>.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body.</param>
    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsync(url, body.AsJson());
    }

    /// <summary>Sends a PUT with <paramref name="body"/> serialized via <see cref="TestJson.Options"/>.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body.</param>
    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PutAsync(url, body.AsJson());
    }

    /// <summary>Sends a PATCH with <paramref name="body"/> serialized via <see cref="TestJson.Options"/>.</summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body.</param>
    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PatchAsync(url, body.AsJson());
    }

    /// <summary>
    /// Reads the response as an <see cref="ApiEnvelope{T}"/> and returns its <c>data</c> payload.
    /// On a non-success status or a missing/empty <c>data</c>, throws with the status code and raw body to ease diagnosis.
    /// </summary>
    /// <param name="response">The HTTP response to unwrap.</param>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <returns>The unwrapped <c>data</c> payload.</returns>
    /// <exception cref="InvalidOperationException">The response was unsuccessful or carried no <c>data</c> payload.</exception>
    public static async Task<T> ReadEnvelopeAsync<T>(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Expected a success envelope of type {typeof(T).Name} but the response was {(int)response.StatusCode} {response.StatusCode}. Body: {Truncate(error)}");
        }

        ApiEnvelope<T>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Could not deserialize the response into an envelope of {typeof(T).Name} ({(int)response.StatusCode} {response.StatusCode}). Body: {Truncate(raw)}", ex);
        }

        return envelope is { Data: { } data }
            ? data
            : throw new InvalidOperationException(
                $"Response {(int)response.StatusCode} {response.StatusCode} had no `data` payload of type {typeof(T).Name}.");
    }

    private static string Truncate(string value, int max = 2000)
        => value.Length <= max ? value : value[..max] + "… (truncated)";
}
