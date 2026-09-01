using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

/// <summary>Records the DbContext types the SDK registered, so their interceptor wiring can be verified at boot.</summary>
internal sealed class EfContextWiringRegistry
{
    private readonly HashSet<Type> _contextTypes = [];
    private readonly HashSet<Type> _configuredBySdk = [];

    /// <summary>Records a DbContext type registered through an SDK registration path.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    public void Register(Type contextType)
    {
        lock (_contextTypes)
            _contextTypes.Add(contextType);
    }

    /// <summary>Records that the SDK's own options callback is the one that built this context's options.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    public void MarkConfiguredBySdk(Type contextType)
    {
        lock (_contextTypes)
            _configuredBySdk.Add(contextType);
    }

    /// <summary>Reports whether the SDK's options callback built this context's live options.</summary>
    /// <param name="contextType">The concrete DbContext type.</param>
    /// <returns><see langword="false"/> when the registration was replaced after the SDK added it.</returns>
    public bool WasConfiguredBySdk(Type contextType)
    {
        lock (_contextTypes)
            return _configuredBySdk.Contains(contextType);
    }

    /// <summary>Returns a snapshot of the recorded DbContext types.</summary>
    /// <returns>The recorded context types.</returns>
    public Type[] Snapshot()
    {
        lock (_contextTypes)
            return [.. _contextTypes];
    }
}
