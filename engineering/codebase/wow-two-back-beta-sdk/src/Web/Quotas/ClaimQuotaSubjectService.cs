using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Provides the default subject: the signed-in user by claim with the plan claim, else the client address.</summary>
/// <param name="options">Claim types and the default plan; <c>Quotas</c> reloads live.</param>
public sealed class ClaimQuotaSubjectService(IOptionsMonitor<QuotaOptions> options) : IQuotaSubjectService
{
    /// <inheritdoc />
    public ValueTask<(string Subject, string? Plan)> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var current = options.CurrentValue;
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && current.SubjectClaimTypes.Select(type => user.FindFirst(type)?.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) is { } id)
        {
            return ValueTask.FromResult<(string, string?)>(("user:" + id, user.FindFirst(current.PlanClaimType)?.Value ?? current.DefaultPlan));
        }

        return ValueTask.FromResult<(string, string?)>(("ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), current.DefaultPlan));
    }
}
