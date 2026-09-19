using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>Holds tuning for <see cref="CloudEventsMessageSerializer"/> — the producer identity and the optional context attributes it stamps.</summary>
/// <remarks>
///   - Register as a singleton before swapping the serializer in
///   - <c>AddMessageSerializer&lt;T&gt;()</c> takes no options argument
///   - the serializer resolves it from DI
/// </remarks>
public sealed record CloudEventsSerializerOptions
{
    /// <summary>
    /// The CloudEvents <c>source</c> — the context in which the event happened; REQUIRED by the spec and, paired with
    /// <c>id</c>, the consumer's duplicate-detection key. Defaults to <c>urn:wow-two:{entry-assembly-name}</c>.
    /// </summary>
    /// <remarks>Set this explicitly in production: the default identifies a process, not a logical service, so it moves when the host does.</remarks>
    public Uri Source { get; set; } = DefaultSource;

    /// <summary>JSON options for the <c>data</c> payload; null uses <see cref="JsonOptionsConstants.Default"/> so the body matches the SDK's JSON default byte-for-byte.</summary>
    public JsonSerializerOptions? Json { get; set; }

    /// <summary>
    /// Produces the CloudEvents <c>id</c> from the body; null generates a fresh GUID per serialize.
    /// </summary>
    /// <remarks>
    ///   - point at a business key on the body when the consumer dedups on <c>source</c>+<c>id</c>
    ///   - the generated <c>id</c> is unreachable afterwards
    ///   - the seam carries no <c>MessageId</c>
    /// </remarks>
    public Func<object, Type, string>? IdFactory { get; set; }

    /// <summary>Optional CloudEvents <c>subject</c> — the specific object within the <see cref="Source"/> the event is about.</summary>
    public string? Subject { get; set; }

    /// <summary>Optional CloudEvents <c>dataschema</c> — a URI identifying the schema the <c>data</c> payload adheres to.</summary>
    public Uri? DataSchema { get; set; }

    /// <summary>Clock for the <c>time</c> attribute; defaults to <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    private static Uri DefaultSource { get; } = new(
        "urn:wow-two:" + (Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown"),
        UriKind.RelativeOrAbsolute);
}
