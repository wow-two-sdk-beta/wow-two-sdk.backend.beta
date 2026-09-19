using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness.Trackers;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The first pluggable interceptor — registered via <c>AddEfInterceptor</c>, so its firing proves the auto-wire loop ran.</summary>
/// <param name="log">The shared invocation log.</param>
public sealed class FirstRecordingInterceptor(InterceptorInvocationTracker log) : RecordingSaveChangesInterceptorBase(log)
{
    /// <summary>Holds the log entry this interceptor writes.</summary>
    public const string Name = "first";

    /// <inheritdoc />
    protected override string Tag => Name;
}
