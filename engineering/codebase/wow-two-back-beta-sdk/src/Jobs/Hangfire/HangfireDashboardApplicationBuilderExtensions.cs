using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

/// <summary>Hangfire dashboard wiring.</summary>
public static class HangfireDashboardApplicationBuilderExtensions
{
    /// <summary>Maps the Hangfire dashboard, locked to local requests unless <paramref name="localRequestsOnly"/> is <c>false</c> behind your own auth.</summary>
    /// <param name="app">The application pipeline.</param>
    /// <param name="path">Dashboard path. Default <c>/jobs</c>.</param>
    /// <param name="localRequestsOnly">Restrict to loopback requests. Default <c>true</c>.</param>
    public static IApplicationBuilder UseHangfireJobsDashboard(
        this IApplicationBuilder app,
        string path = "/jobs",
        bool localRequestsOnly = true)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var options = new DashboardOptions();
        if (localRequestsOnly)
        {
            options.Authorization = [new LocalRequestsOnlyAuthorizationFilter()];
        }

        return app.UseHangfireDashboard(path, options);
    }
}
