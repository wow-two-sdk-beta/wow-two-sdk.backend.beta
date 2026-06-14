using WireMock.Server;
using WireMock.Settings;

namespace WoW.Two.Sdk.Backend.Beta.Testing.WireMock;

/// <summary>
/// Async fixture starting an in-process <see cref="WireMockServer"/> on a random port.
/// </summary>
public sealed class WireMockFixture : IAsyncTestFixture
{
    /// <summary>The underlying WireMock server (only available after <see cref="StartAsync"/>).</summary>
    public WireMockServer Server { get; private set; } = default!;

    /// <inheritdoc />
    public string Name => "wiremock";

    /// <summary>HTTP base URL of the started server.</summary>
    public string Url => Server.Url
        ?? throw new InvalidOperationException("WireMock server has not started.");

    /// <inheritdoc />
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        Server = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = false,
            StartAdminInterface = true,
        });
        return ValueTask.CompletedTask;
    }

    /// <summary>Resets all stubs and request history. Default <see cref="ResetAsync"/> behavior.</summary>
    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        Server?.Reset();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Server?.Stop();
        Server?.Dispose();
        return ValueTask.CompletedTask;
    }
}
