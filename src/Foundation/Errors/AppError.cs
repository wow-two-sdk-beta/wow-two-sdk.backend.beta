using System.Runtime.CompilerServices;
using Ardalis.GuardClauses;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Represents an expected, transport-agnostic failure — returned via a result or thrown via <see cref="AppException"/>.</summary>
public record AppError
{
    /// <summary>Gets the abstract kind of the failure.</summary>
    public required AppErrorType Type { get; init; }

    /// <summary>Gets the human-readable, default-culture message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the optional message-template arguments and diagnostic context surfaced as ProblemDetails extensions.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    /// <summary>Gets the source location that created the error.</summary>
    public ErrorOrigin? Origin { get; init; }

    /// <summary>Creates an error of <paramref name="type"/>, capturing the call site into <see cref="Origin"/>.</summary>
    /// <param name="type">The failure kind.</param>
    /// <param name="message">The default-culture message.</param>
    /// <param name="metadata">Optional message arguments and diagnostic context.</param>
    /// <param name="member">The calling member, captured automatically.</param>
    /// <param name="file">The calling source file, captured automatically.</param>
    /// <param name="line">The calling source line, captured automatically.</param>
    public static AppError Of(
        AppErrorType type,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        Guard.Against.NullOrWhiteSpace(message);

        var origin = new ErrorOrigin { Member = member, File = file, Line = line };

        return new AppError { Type = type, Message = message, Metadata = metadata, Origin = origin };
    }

    /// <summary>Creates an error from a caught <paramref name="exception"/>, deriving <see cref="Origin"/> from its throw site; the live exception is not retained.</summary>
    /// <param name="type">The failure kind.</param>
    /// <param name="message">The default-culture message.</param>
    /// <param name="exception">The caught exception whose stack and type are folded in.</param>
    public static AppError FromException(AppErrorType type, string message, Exception exception)
    {
        Guard.Against.NullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(exception);

        var metadata = new Dictionary<string, object?>
        {
            ["cause"] = exception.GetType().Name,
            ["causeChain"] = ExceptionChain.Flatten(exception),
        };

        return new AppError { Type = type, Message = message, Metadata = metadata, Origin = ErrorOrigin.FromException(exception) };
    }

    /// <summary>Returns a value indicating whether this error is of <paramref name="type"/>.</summary>
    /// <param name="type">The failure kind to compare against.</param>
    public bool Is(AppErrorType type)
    {
        return Type == type;
    }

    /// <summary>Wraps the error in an <see cref="AppException"/> for the throw path, preserving <paramref name="inner"/> natively.</summary>
    /// <param name="inner">Optional underlying cause to preserve on the exception.</param>
    public AppException ToException(Exception? inner = null)
    {
        return new AppException(this, inner);
    }

    /// <summary>Throws this error as an <see cref="AppException"/>.</summary>
    /// <param name="inner">Optional underlying cause to preserve on the exception.</param>
    public void Throw(Exception? inner = null)
    {
        throw ToException(inner);
    }
}
