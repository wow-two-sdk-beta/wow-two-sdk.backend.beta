using System.Runtime.CompilerServices;
using VerifyTests;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Verify;

/// <summary>
/// Conventional Verify defaults applied for the Wow Two backend SDK:
/// <list type="bullet">
///   <item>Sub-folder per test class (`UseDirectory("Snapshots")`).</item>
///   <item>Counter-name suffix for stable ordering.</item>
///   <item>Pre-registered scrubbers for <c>traceId</c>, <c>spanId</c>, <c>requestId</c>, ULID/GUID values, RFC-3339 timestamps.</item>
/// </list>
///
/// Wire by adding to a `ModuleInitializer` in the consumer test project:
/// <code>
/// [ModuleInitializer]
/// public static void Init() => VerifyDefaults.Initialize();
/// </code>
/// </summary>
public static class VerifyDefaults
{
    private static int _initialized;

    /// <summary>Apply default Verify settings. Idempotent.</summary>
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;

        VerifierSettings.UseStrictJson();
        VerifierSettings.AddScrubber(s => s
            .Replace("\"traceId\":\"", "\"traceId\":\""));

        VerifierSettings.ScrubInlineGuids();
        VerifierSettings.ScrubInlineDateTimes("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        VerifierSettings.ScrubInlineDateTimes("o");
    }
}
