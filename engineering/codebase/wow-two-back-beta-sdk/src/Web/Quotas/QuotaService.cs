using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Provides quota counting over <see cref="IQuotaRepository"/>, one counter per quota, window and subject.</summary>
/// <param name="repository">The counters.</param>
/// <param name="options">The quotas; <c>Quotas</c> reloads live.</param>
/// <param name="time">The clock windows are cut from.</param>
public sealed class QuotaService(IQuotaRepository repository, IOptionsMonitor<QuotaOptions> options, TimeProvider time) : IQuotaService
{
    /// <inheritdoc />
    public async Task<QuotaResult> TryConsumeAsync(string quota, string subject, string? plan = null, long amount = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        var (_, limit, key, endsAt) = Resolve(quota, subject, plan);
        var used = await repository.TryIncrementAsync(key, amount, limit, endsAt?.AddMinutes(5), cancellationToken);
        if (used is { } count)
            return new QuotaResult { Quota = quota, Allowed = true, Limit = limit, Used = count, ResetsAt = endsAt };

        return new QuotaResult { Quota = quota, Allowed = false, Limit = limit, Used = await repository.GetAsync(key, cancellationToken), ResetsAt = endsAt };
    }

    /// <inheritdoc />
    public Task RefundAsync(string quota, string subject, long amount = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return repository.DecrementAsync(Resolve(quota, subject, null).Key, amount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotaResult> GetUsageAsync(string quota, string subject, string? plan = null, CancellationToken cancellationToken = default)
    {
        var (_, limit, key, endsAt) = Resolve(quota, subject, plan);
        return new QuotaResult { Quota = quota, Allowed = true, Limit = limit, Used = await repository.GetAsync(key, cancellationToken), ResetsAt = endsAt };
    }

    private (QuotaDefinitionOptions Definition, long? Limit, string Key, DateTimeOffset? EndsAt) Resolve(string quota, string subject, string? plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quota);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var current = options.CurrentValue;
        var definition = current.Definitions.TryGetValue(quota, out var found)
            ? found
            : throw new InvalidOperationException($"No quota '{quota}' is defined under {QuotaOptions.SectionName}:Definitions.");
        var (window, endsAt) = QuotaPeriodMapper.Window(definition.Period, time.GetUtcNow());
        return (definition, definition.LimitFor(plan ?? current.DefaultPlan), $"{quota.ToLowerInvariant()}:{window}:{subject}", endsAt);
    }
}
