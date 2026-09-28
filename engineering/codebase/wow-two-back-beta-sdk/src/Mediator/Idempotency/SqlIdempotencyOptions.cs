using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Holds the durable idempotency table, the in-progress lease and the response serializer settings.</summary>
public sealed record SqlIdempotencyOptions
{
    /// <summary>Table holding the records; letters, digits, underscores and one schema dot. Default <c>idempotency_records</c>.</summary>
    public string TableName { get; set; } = "idempotency_records";

    /// <summary>How long an in-progress key blocks duplicates before another host may take it over. Default 5 minutes.</summary>
    public TimeSpan PendingLease { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Serializer settings for stored responses; each response must round-trip as its declared type. Default web settings.</summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = new(JsonSerializerDefaults.Web);
}
