namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the abstract kind of an <see cref="AppError"/> — the classifier mapped to a transport status at the edge.</summary>
public enum AppErrorType
{
    /// <summary>Unexpected server-side failure with no more specific kind.</summary>
    Unexpected,

    /// <summary>Caller-supplied input failed validation.</summary>
    Validation,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state.</summary>
    Conflict,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not permitted.</summary>
    Forbidden,

    /// <summary>The caller exceeded a rate limit.</summary>
    TooManyRequests,

    /// <summary>A database operation exceeded its time budget.</summary>
    DbTimeout,

    /// <summary>An in-process operation exceeded its time budget.</summary>
    OperationTimeout,

    /// <summary>An external dependency rejected our credentials.</summary>
    ExternalUnauthorized,

    /// <summary>An external dependency is unreachable or unavailable.</summary>
    ExternalUnavailable,

    /// <summary>A required file was not found.</summary>
    FileNotFound,

    /// <summary>Serialization or deserialization failed.</summary>
    SerializationFailed,

    /// <summary>The persisted data violated an integrity expectation.</summary>
    DataIntegrity,

    /// <summary>The request is well-formed but violates a business rule. Maps to 422.</summary>
    BusinessRule,

    /// <summary>The action requires payment or a higher plan. Maps to 402.</summary>
    PaymentRequired,

    /// <summary>The resource existed but is permanently gone (expired or revoked). Maps to 410.</summary>
    Gone,

    /// <summary>The operation was canceled by the caller. Maps to 499.</summary>
    Canceled,
}
