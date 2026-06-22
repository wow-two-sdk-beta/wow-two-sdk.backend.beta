namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the canonical error category, mapped to an HTTP status code.</summary>
public enum DomainErrorCategory
{
    /// <summary>Caller sent invalid input. Maps to 400.</summary>
    Validation = 400,

    /// <summary>Caller is unauthenticated. Maps to 401.</summary>
    Unauthorized = 401,

    /// <summary>Caller is authenticated but lacks permission. Maps to 403.</summary>
    Forbidden = 403,

    /// <summary>Resource not found. Maps to 404.</summary>
    NotFound = 404,

    /// <summary>Conflicting state. Maps to 409.</summary>
    Conflict = 409,

    /// <summary>Domain rule violation (semantically valid request, business state rejects it). Maps to 422.</summary>
    BusinessRule = 422,

    /// <summary>Rate limit exceeded. Maps to 429.</summary>
    TooManyRequests = 429,

    /// <summary>Unexpected server error. Maps to 500.</summary>
    Unexpected = 500,

    /// <summary>Service temporarily unavailable — a required dependency or resource is down, locked, or sealed. Maps to 503.</summary>
    Unavailable = 503,
}

/// <summary>Represents an immutable, HTTP-aware domain error.</summary>
/// <param name="Code">Stable code (e.g. <c>orders.not_found</c>).</param>
/// <param name="Message">Human-readable message. Localized when possible.</param>
/// <param name="Category">Category controlling default HTTP mapping.</param>
/// <param name="Detail">Optional additional context.</param>
public sealed record DomainError(
    string Code,
    string Message,
    DomainErrorCategory Category,
    string? Detail = null)
{
    /// <summary>HTTP status code derived from <see cref="Category"/>.</summary>
    public int StatusCode => (int)Category;

    /// <summary>Convenience factory — validation error.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Validation(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Validation, detail);

    /// <summary>Convenience factory — not-found.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError NotFound(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.NotFound, detail);

    /// <summary>Convenience factory — conflict.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Conflict(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Conflict, detail);

    /// <summary>Convenience factory — forbidden.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Forbidden(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Forbidden, detail);

    /// <summary>Convenience factory — unauthorized.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Unauthorized(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Unauthorized, detail);

    /// <summary>Convenience factory — business-rule violation.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError BusinessRule(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.BusinessRule, detail);

    /// <summary>Convenience factory — unexpected server error.</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Unexpected(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Unexpected, detail);

    /// <summary>Convenience factory — service unavailable (dependency down, resource locked/sealed).</summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">The human-readable message.</param>
    /// <param name="detail">Optional additional context.</param>
    public static DomainError Unavailable(string code, string message, string? detail = null) =>
        new(code, message, DomainErrorCategory.Unavailable, detail);
}
