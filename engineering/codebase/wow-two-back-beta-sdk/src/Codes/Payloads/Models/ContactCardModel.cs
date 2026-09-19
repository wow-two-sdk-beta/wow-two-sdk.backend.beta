namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Represents supplied data for ContactCard encoding.</summary>
public sealed record ContactCardModel
{
    /// <summary>Gets the FirstName value.</summary>
    public string? FirstName { get; init; }

    /// <summary>Gets the LastName value.</summary>
    public string? LastName { get; init; }

    /// <summary>Gets the Organization value.</summary>
    public string? Organization { get; init; }

    /// <summary>Gets the Title value.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the Phone value.</summary>
    public string? Phone { get; init; }

    /// <summary>Gets the Email value.</summary>
    public string? Email { get; init; }

    /// <summary>Gets the Url value.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the Address value.</summary>
    public string? Address { get; init; }

    /// <summary>Gets the Note value.</summary>
    public string? Note { get; init; }
}
