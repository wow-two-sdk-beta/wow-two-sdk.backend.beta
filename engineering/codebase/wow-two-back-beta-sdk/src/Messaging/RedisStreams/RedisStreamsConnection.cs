using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>
/// The adapter's connection to Redis, shared by the send and receive halves. One multiplexer, because that is what
/// StackExchange.Redis is built for — it is thread-safe, pipelines concurrent callers, and reconnects on its own — and
/// because nothing here blocks a connection the way a <c>BLOCK</c>-ing read would.
/// </summary>
internal sealed class RedisStreamsConnection(RedisStreamsOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private ConnectionMultiplexer? _multiplexer; // concrete: CA1859 — this field is only ever assigned ConnectionMultiplexer.ConnectAsync

    /// <summary>The configured database, connecting on first use.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        // Connect lazily, so DI graph construction does no I/O and an unreachable Redis fails the first send.
        if (_multiplexer is { } connected)
            return connected.GetDatabase(options.Database);

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            _multiplexer ??= await ConnectionMultiplexer.ConnectAsync(options.Configuration);
            return _multiplexer.GetDatabase(options.Database);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Close the connection before the gate, so a caller racing shutdown sees an ordinary connection error.
        if (_multiplexer is { } multiplexer)
        {
            _multiplexer = null;
            await multiplexer.CloseAsync();
            multiplexer.Dispose();
        }

        _connectGate.Dispose();
    }
}
