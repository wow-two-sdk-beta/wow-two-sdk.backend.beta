using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness.Trackers;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The second pluggable interceptor — its position relative to <see cref="FirstRecordingInterceptor"/> is the ordering assertion.</summary>
/// <param name="log">The shared invocation log.</param>
public sealed class SecondRecordingInterceptor(InterceptorInvocationTracker log) : RecordingSaveChangesInterceptorBase(log)
{
    /// <summary>Holds the log entry this interceptor writes.</summary>
    public const string Name = "second";

    /// <inheritdoc />
    protected override string Tag => Name;
}
