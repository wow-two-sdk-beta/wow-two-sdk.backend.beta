using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>Ensures the JetStream stream (and durable consumer) exist — idempotent; ignores "already exists".</summary>
internal sealed class NatsTopologyBroker
{
    /// <summary>JetStream's "stream not found" — the API answers a STREAM.INFO for an absent stream with a 404.</summary>
    private const int StreamNotFoundCode = 404;

    public async ValueTask EnsureStreamAsync(NatsJSContext js, NatsOptions options, CancellationToken cancellationToken)
    {
        var subjects = StreamSubjects(options);

        // Unrouted: create and swallow — an existing stream is never rewritten, so its settings cannot move.
        if (!options.RouteByDestination)
        {
            await TryCreateStreamAsync(js, options.Stream, subjects, cancellationToken);
            return;
        }

        // Routed: widen the stream in place — JetStream rejects a publish to a subject no stream claims.
        StreamConfig? existing;
        try
        {
            existing = (await js.GetStreamAsync(options.Stream, cancellationToken: cancellationToken)).Info.Config;
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == StreamNotFoundCode)
        {
            existing = null;
        }

        if (existing is null)
        {
            await TryCreateStreamAsync(js, options.Stream, subjects, cancellationToken);
            return;
        }

        // Read-modify-write, so the operator's retention, max-age and replica settings survive the widening.
        var merged = MergeSubjects(existing.Subjects, subjects, options.Subject);
        if (merged is null)
            return;

        existing.Subjects = merged;
        await js.UpdateStreamAsync(existing, cancellationToken);
    }

    public async ValueTask EnsureConsumerAsync(
        NatsJSContext js,
        NatsOptions options,
        IReadOnlyList<string> consumeSubjects,
        CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig(options.DurableConsumer)
        {
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            MaxDeliver = options.MaxDeliver,
        };

        // Exactly one filter form may be set — the server rejects a consumer carrying both.
        if (options.RouteByDestination)
            config.FilterSubjects = [.. consumeSubjects];
        else
            config.FilterSubject = options.Subject;

        await js.CreateOrUpdateConsumerAsync(options.Stream, config, cancellationToken);
    }

    /// <summary>The subjects the stream must claim: the root, its wildcard when routing, and the dead-letter subject unless the wildcard already covers it.</summary>
    private List<string> StreamSubjects(NatsOptions options)
    {
        var subjects = new List<string>(3) { options.Subject };

        if (options.RouteByDestination)
            subjects.Add(options.Subject + ".>");

        // The server rejects two stream entries overlapping one subject space, so skip what the wildcard covers.
        if (!options.RouteByDestination || !NatsSubjectNameMapper.IsRootedAt(options.DeadLetterSubject, options.Subject))
            subjects.Add(options.DeadLetterSubject);

        return subjects;
    }

    /// <summary>The stream's subjects widened with <paramref name="required"/>, or null when it already covers them.</summary>
    private List<string>? MergeSubjects(ICollection<string>? current, List<string> required, string root)
    {
        // Test `required` first, or the rooted wildcard is dropped and re-added and reports a change on every start.
        var merged = new List<string>();
        var changed = false;
        if (current is not null)
        {
            foreach (var subject in current)
            {
                if (!required.Contains(subject, StringComparer.Ordinal) && NatsSubjectNameMapper.IsRootedAt(subject, root))
                {
                    changed = true;
                    continue;
                }

                merged.Add(subject);
            }
        }

        foreach (var subject in required)
        {
            if (merged.Contains(subject, StringComparer.Ordinal))
                continue;

            merged.Add(subject);
            changed = true;
        }

        return changed ? merged : null;
    }

    private async ValueTask TryCreateStreamAsync(NatsJSContext js, string stream, ICollection<string> subjects, CancellationToken cancellationToken)
    {
        try
        {
            await js.CreateStreamAsync(new StreamConfig(stream, subjects), cancellationToken);
        }
        catch (NatsJSApiException)
        {
            // Stream already provisioned (name in use, or another instance won the race) — treat as ready.
        }
    }
}
