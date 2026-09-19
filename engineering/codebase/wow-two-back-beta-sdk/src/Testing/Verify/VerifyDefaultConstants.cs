using System.Runtime.CompilerServices;
using VerifyTests;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Verify;

/// <summary>
/// Holds conventional Verify defaults applied for the Wow Two backend SDK:
/// - Sub-folder per test class (`UseDirectory("Snapshots")`).
/// - Counter-name suffix for stable ordering.
/// - Pre-registered scrubbers for <c>traceId</c>, <c>spanId</c>, <c>requestId</c>, ULID/GUID values, RFC-3339 timestamps.
///
/// Wire by adding to a `ModuleInitializer` in the consumer test project:
/// <code>
/// [ModuleInitializer]
/// public static void Init() => VerifyDefaultConstants.Initialize();
/// </code>
/// </summary>
public static class VerifyDefaultConstants
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
