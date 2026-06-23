using System.Diagnostics;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents the source location where an <see cref="AppError"/> was created — diagnostics only, never serialized to a client.</summary>
public sealed record ErrorOrigin
{
    /// <summary>Gets the member that created the error.</summary>
    public required string Member { get; init; }

    /// <summary>Gets the source file path, when available (requires debug symbols).</summary>
    public string? File { get; init; }

    /// <summary>Gets the source line, when available (requires debug symbols).</summary>
    public int? Line { get; init; }

    /// <summary>Builds an origin from the throw site of <paramref name="exception"/> — member always, file and line when symbols are present.</summary>
    /// <param name="exception">The exception whose deepest stack frame locates the throw site.</param>
    public static ErrorOrigin FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var frame = new StackTrace(exception, fNeedFileInfo: true).GetFrame(0);
        var member = frame?.GetMethod()?.Name ?? exception.TargetSite?.Name ?? "unknown";

        return new ErrorOrigin { Member = member, File = frame?.GetFileName(), Line = frame?.GetFileLineNumber() };
    }
}
