using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Holds the quotas and how a request's subject and plan are found.</summary>
/// <remarks>
/// Off until <see cref="Enabled"/>: gates pass and nothing counts. Set in code with <c>AddQuotas(o => …)</c> or in the
/// host section <c>Quotas</c>, which is applied last.
/// </remarks>
public sealed record QuotaOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Quotas";

    /// <summary>Gets or sets whether quotas count and gate. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets the quotas by name, such as <c>conversions</c>; names ignore case.</summary>
    public Dictionary<string, QuotaDefinitionOptions> Definitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the claim naming a signed-in user's plan. Default <c>plan</c>.</summary>
    public string PlanClaimType { get; set; } = "plan";

    /// <summary>Gets or sets the plan of anonymous callers and users without the claim. Default <c>free</c>.</summary>
    public string DefaultPlan { get; set; } = "free";

    /// <summary>Gets the claims that identify a user, first match wins; anonymous callers count by client address.</summary>
    public List<string> SubjectClaimTypes { get; } = ["sub", ClaimTypes.NameIdentifier];
}
