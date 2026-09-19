namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;

/// <summary>Represents supplied data for FloatingCalendarEvent encoding.</summary>
public sealed record FloatingCalendarEventModel
{
    /// <summary>Gets the Title value.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the Start value.</summary>
    public NodaTime.LocalDateTime Start { get; init; }

    /// <summary>Gets the End value.</summary>
    public NodaTime.LocalDateTime End { get; init; }

    /// <summary>Gets the Location value.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the Description value.</summary>
    public string? Description { get; init; }
}
