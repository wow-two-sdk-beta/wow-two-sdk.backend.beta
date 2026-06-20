namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Defines the category of an application failure, used to resolve an HTTP status code.</summary>
public enum FailureCategory
{
    /// <summary>Unexpected or unclassified failure → 500.</summary>
    Unexpected = 0,

    /// <summary>Input failed validation → 400.</summary>
    Validation,

    /// <summary>Requested resource does not exist → 404.</summary>
    NotFound,

    /// <summary>Conflicts with current state, e.g. a duplicate → 409.</summary>
    Conflict,

    /// <summary>Authentication required or failed → 401.</summary>
    Unauthorized,

    /// <summary>Authenticated but not permitted → 403.</summary>
    Forbidden,

    /// <summary>Payment is required to proceed → 402.</summary>
    PaymentRequired,

    /// <summary>The service or a required dependency is temporarily unavailable → 503.</summary>
    Unavailable
}
