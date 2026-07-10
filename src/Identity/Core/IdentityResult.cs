namespace WoW.Two.Sdk.Backend.Beta.Identity.Core;

/// <summary>A single failure from an identity operation.</summary>
/// <param name="Code">Stable error code (e.g. <c>DuplicateUserName</c>).</param>
/// <param name="Description">Human-readable description.</param>
public readonly record struct IdentityError(string Code, string Description);

/// <summary>The outcome of an identity operation — success, or a list of failures.</summary>
public sealed class IdentityResult
{
    private static readonly IdentityResult SuccessResult = new(true, []);

    private IdentityResult(bool succeeded, IReadOnlyList<IdentityError> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>The failures when <see cref="Succeeded"/> is false; empty otherwise.</summary>
    public IReadOnlyList<IdentityError> Errors { get; }

    /// <summary>The shared success result.</summary>
    public static IdentityResult Success => SuccessResult;

    /// <summary>Create a failed result from one or more errors.</summary>
    /// <param name="errors">The failures.</param>
    public static IdentityResult Failed(params IdentityError[] errors) => new(false, errors ?? []);
}
