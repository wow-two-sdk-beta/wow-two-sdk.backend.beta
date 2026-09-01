using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Harness;

/// <summary>The second pluggable interceptor — its position relative to <see cref="FirstRecordingInterceptor"/> is the ordering assertion.</summary>
/// <param name="log">The shared invocation log.</param>
public sealed class SecondRecordingInterceptor(InterceptorLog log) : RecordingSaveChangesInterceptorBase(log)
{
    /// <summary>The log entry this interceptor writes.</summary>
    public const string Name = "second";

    /// <inheritdoc />
    protected override string Tag => Name;
}
