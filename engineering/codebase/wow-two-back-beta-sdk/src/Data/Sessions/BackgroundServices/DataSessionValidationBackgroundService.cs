using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions.BackgroundServices;

/// <summary>Runs data-session provider validation at host startup without opening a connection.</summary>
internal sealed class DataSessionValidationBackgroundService(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<IDataSession>();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
