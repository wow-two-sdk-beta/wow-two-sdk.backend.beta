using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// Strategy for claiming the next batch of pending outbox rows — the seam that lets multi-instance dispatch swap in
/// a locking claim (Postgres <c>FOR UPDATE SKIP LOCKED</c>) without changing the dispatcher. The default polls without
/// locking (single-instance safe).
/// </summary>
public interface IOutboxClaimRepository
{
    /// <summary>Claim up to <paramref name="batchSize"/> pending rows for this dispatcher to process.</summary>
    /// <param name="context">The outbox DbContext.</param>
    /// <param name="batchSize">Maximum rows to claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<OutboxMessageEntity>> ClaimPendingAsync(DbContext context, int batchSize, CancellationToken cancellationToken);
}
