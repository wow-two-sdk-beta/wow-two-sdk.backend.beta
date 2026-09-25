using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Data.Sessions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;
using WoW.Two.Sdk.Backend.Beta.Tenancy.Core;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;

/// <summary>Deduplicates successful operations within request, tenant and principal scope.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The response contract.</typeparam>
/// <param name="store">The ownership and response store.</param>
/// <param name="session">An optional transaction owner delaying cached success until commit.</param>
/// <param name="tenant">Optional server-owned tenant context.</param>
/// <param name="currentUser">Optional server-owned principal context.</param>
public sealed class DeduplicatingInterceptor<TRequest, TResponse>(
    IIdempotencyRepository store,
    IDataSession? session = null,
    ITenantContext? tenant = null,
    ICurrentUserService? currentUser = null) : IRequestInterceptor<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Gets or sets the replay lifetime for this closed request contract. Defaults to one day.</summary>
    public static TimeSpan Ttl { get; set; } = TimeSpan.FromDays(1);

    /// <inheritdoc />
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> nextStep,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(nextStep);
        if (request is not IIdempotent idempotent)
        {
            return await nextStep().ConfigureAwait(false);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotent.IdempotencyKey);
        string key = JsonSerializer.Serialize(new[]
        {
            typeof(TRequest).AssemblyQualifiedName,
            tenant?.TenantId,
            currentUser?.Kind.ToString(),
            currentUser?.Id?.ToString(),
            idempotent.IdempotencyKey
        });
        var (acquired, cached, ownership) = await store.TryAcquireAsync(key, typeof(TResponse), cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            if (cached is TResponse response)
            {
                return response;
            }
            if (cached is null && default(TResponse) is null)
            {
                return default!;
            }
            throw new InvalidOperationException("The cached idempotent response has an incompatible type.");
        }

        bool deferred = false;
        try
        {
            TResponse response = await nextStep().ConfigureAwait(false);
            if (response is IResult { IsSuccess: false }
                || session?.State is DataSessionState.RolledBack or DataSessionState.Faulted or DataSessionState.Disposed)
            {
                return response;
            }
            if (session?.State == DataSessionState.Active)
            {
                session.OnRolledBack(_ => new ValueTask(store.ReleaseAsync(key, ownership, CancellationToken.None)));
                await session.OnCommittedAsync(token =>
                    new ValueTask(store.StoreAsync(key, ownership, response, Ttl, token))).ConfigureAwait(false);
                deferred = true;
            }
            else
            {
                // A completed effect retains ownership if replay publication fails; retrying it could duplicate the effect.
                deferred = true;
                await store.StoreAsync(key, ownership, response, Ttl, CancellationToken.None).ConfigureAwait(false);
            }
            return response;
        }
        finally
        {
            if (!deferred)
            {
                await store.ReleaseAsync(key, ownership, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
