namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Defines quota counting for a subject — a user, an API key, an address — under its plan.</summary>
public interface IQuotaService
{
    /// <summary>Uses <paramref name="amount"/> units when they fit this window; otherwise uses none and reports why.</summary>
    /// <param name="quota">The quota name.</param>
    /// <param name="subject">Who is counted, such as <c>user:42</c>.</param>
    /// <param name="plan">The subject's plan; null takes the default plan.</param>
    /// <param name="amount">The units, from 1.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The quota is not defined.</exception>
    Task<QuotaResult> TryConsumeAsync(string quota, string subject, string? plan = null, long amount = 1, CancellationToken cancellationToken = default);

    /// <summary>Returns units to this window, such as after the counted work failed.</summary>
    /// <param name="quota">The quota name.</param>
    /// <param name="subject">Who was counted.</param>
    /// <param name="amount">The units.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefundAsync(string quota, string subject, long amount = 1, CancellationToken cancellationToken = default);

    /// <summary>The subject's standing on one quota without using any.</summary>
    /// <param name="quota">The quota name.</param>
    /// <param name="subject">Who is counted.</param>
    /// <param name="plan">The subject's plan; null takes the default plan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<QuotaResult> GetUsageAsync(string quota, string subject, string? plan = null, CancellationToken cancellationToken = default);
}
