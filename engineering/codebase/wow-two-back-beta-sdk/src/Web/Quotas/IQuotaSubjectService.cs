using Microsoft.AspNetCore.Http;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Defines who a request counts against and under which plan; replace it to read plans from the product's data.</summary>
public interface IQuotaSubjectService
{
    /// <summary>The request's subject, such as <c>user:42</c> or <c>ip:203.0.113.7</c>, and its plan.</summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<(string Subject, string? Plan)> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
