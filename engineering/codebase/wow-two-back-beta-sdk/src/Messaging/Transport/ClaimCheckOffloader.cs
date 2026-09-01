using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// The send half: serializes the body once, and where it exceeds the threshold writes it to blob storage and puts the
/// small reference on the wire in its place.
/// </summary>
/// <remarks>
///   - runs on every publish from <see cref="TransportEventBus"/>, before the envelope reaches an adapter
///   - substitutes <see cref="EventEnvelope.RawBody"/> and <see cref="EventEnvelope.RawBodyType"/> only
///   - <see cref="EventEnvelope.BodyType"/> keeps <see cref="ITopologyService.ResolveRoutingKey"/> routing by the real contract
/// </remarks>
internal sealed partial class ClaimCheckOffloader(
    ClaimCheckPayloadRepository store,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ClaimCheckOptions options,
    ILogger<ClaimCheckOffloader> logger)
{
    private readonly ClaimCheckOptions _options = options;

    /// <summary>True when oversized bodies are offloaded. False leaves the send path putting every body inline.</summary>
    public bool IsActive => _options.Enabled;

    /// <summary>
    /// Decide what <paramref name="envelope"/> puts on the wire: the body's own bytes, or — over the threshold — a
    /// reference to them in blob storage. Returns the same instance untouched whenever there is nothing to do, so the
    /// caller can assign the result unconditionally.
    /// </summary>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<EventEnvelope> PrepareAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Off, or an earlier transformation already fixed the wire bytes and owns what travels.
        if (!IsActive || envelope.RawBody is not null)
            return envelope;

        // An already-offloaded re-publish carries the pointer — offloading it again would store a reference to a reference.
        if (ClaimCheckHeaderConstants.TryReadReference(envelope, out _))
            return envelope;

        // Serialize once — under the threshold these bytes are the wire body, over it they are what gets stored.
        var body = serializer.Serialize(envelope.Body, envelope.BodyType);
        if (body.LongLength <= _options.ThresholdBytes)
            return envelope with { RawBody = new ReadOnlyMemory<byte>(body) };

        var bodyToken = typeResolver.ToTypeToken(envelope.BodyType);
        var path = await store.WriteAsync(body, serializer.ContentType, cancellationToken);
        var reference = new ClaimCheckReference
        {
            Path = path,
            SizeBytes = body.LongLength,
            ContentType = serializer.ContentType,
            BodyType = bodyToken,
        };

        // An adapter copies caller headers minus the keys it re-derives, so these three reach the wire untouched.
        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [ClaimCheckHeaderConstants.Reference] = path,
            [ClaimCheckHeaderConstants.Size] = reference.SizeBytes.ToString(CultureInfo.InvariantCulture),
            [ClaimCheckHeaderConstants.BodyType] = bodyToken,
        };

        LogOffloaded(envelope.MessageId, reference.SizeBytes, path);

        // Body and BodyType stay the real event, so routing, metrics and observers still describe what was published.
        return envelope with
        {
            RawBody = new ReadOnlyMemory<byte>(serializer.Serialize(reference, typeof(ClaimCheckReference))),
            RawBodyType = typeof(ClaimCheckReference),
            Headers = headers,
        };
    }

    [LoggerMessage(EventId = 6081, Level = LogLevel.Debug, Message = "Offloaded the {SizeBytes}-byte body of message {MessageId} to claim check {Path}")]
    private partial void LogOffloaded(string messageId, long sizeBytes, string path);
}
