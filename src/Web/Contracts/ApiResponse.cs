using System.Net;

namespace WoW.Two.Sdk.Backend.Beta.Web.Contracts;

/// <summary>Represents the base API response envelope — shared constants only.</summary>
public record ApiResponse
{
    /// <summary>Title for the fall-through problem response of an unmatched result.</summary>
    public const string UnexpectedErrorMessage = "Unexpected error";
}

/// <summary>Represents the result-pattern HTTP success envelope — a closed union carrying the payload under <c>.data</c>.</summary>
/// <remarks>Servers emit only the success case via <see cref="Ok"/>; errors go out as RFC-7807 ProblemDetails, never here.</remarks>
/// <typeparam name="T">The payload type — always a DTO.</typeparam>
public abstract record ApiResponse<T> : ApiResponse
{
    private ApiResponse()
    {
    }

    /// <summary>Wraps <paramref name="data"/> in a success envelope — the only way to build a success body.</summary>
    /// <param name="data">The payload to wrap.</param>
    public static Success Ok(T data)
    {
        return new Success { Data = data };
    }

    /// <summary>Represents a successful response carrying the typed payload.</summary>
    public sealed record Success : ApiResponse<T>
    {
        /// <summary>Gets the response payload, serialized under <c>.data</c>.</summary>
        public required T Data { get; init; }
    }

    /// <summary>Represents a failed response — the client-side shape for deserializing a non-2xx response.</summary>
    public sealed record Failure : ApiResponse<T>
    {
        /// <summary>Gets the HTTP status code from the response.</summary>
        public required HttpStatusCode StatusCode { get; init; }

        /// <summary>Gets the error description from the failed response.</summary>
        public required string Error { get; init; }
    }
}
