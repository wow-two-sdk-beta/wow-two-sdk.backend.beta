using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>One request waiting for its response, and the contract that response has to satisfy.</summary>
internal sealed class PendingRequest(Type responseType)
{
    private readonly TaskCompletionSource<EventEnvelopeModel> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The response contract this request expects. A message of any other type is not this request's reply.</summary>
    public Type ResponseType { get; } = responseType;

    /// <summary>Completes with the reply envelope.</summary>
    public Task<EventEnvelopeModel> Completion => _completion.Task;

    /// <summary>Complete the request with a reply; false when it was already completed (a duplicate reply).</summary>
    public bool TryComplete(EventEnvelopeModel envelope) => _completion.TrySetResult(envelope);
}
