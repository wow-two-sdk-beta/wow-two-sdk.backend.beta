using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Extensions;
using NodaTime;
using NodaTime.Text;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Exporters;

/// <summary>Exports a timezone-free calendar event as a floating VEVENT fragment.</summary>
public sealed class FloatingCalendarEventExporter : IFloatingCalendarEventExporter
{
    private static readonly LocalDateTimePattern Pattern = LocalDateTimePattern.CreateWithInvariantCulture("uuuuMMdd'T'HHmmss");

    /// <summary>Exports one event without inferring timezone, UTC conversion or calendar delivery policy.</summary>
    public Result<string> Export(FloatingCalendarEventModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Start.Calendar != CalendarSystem.Iso || model.Start.Year < 1
            || model.End is { } end && (end.Calendar != CalendarSystem.Iso || end.Year < 1))
            return Result<string>.Fail(ValidationError.From([new FieldError { Property = "Start", Message = "Use positive ISO calendar years.", Code = "CalendarEventDate" }]));
        if (model.End is { } endTime && endTime <= model.Start)
            return Result<string>.Fail(ValidationError.From([new FieldError { Property = "End", Message = "End must follow start.", Code = "CalendarEventRange" }]));
        var lines = new List<string> { "BEGIN:VEVENT", "SUMMARY:" + ContentLineExtensions.Escape(model.Title),
            "DTSTART:" + Pattern.Format(model.Start) };
        if (model.End is { } suppliedEnd) lines.Add("DTEND:" + Pattern.Format(suppliedEnd));
        if (!string.IsNullOrWhiteSpace(model.Location)) lines.Add("LOCATION:" + ContentLineExtensions.Escape(model.Location));
        if (!string.IsNullOrWhiteSpace(model.Description)) lines.Add("DESCRIPTION:" + ContentLineExtensions.Escape(model.Description));
        lines.Add("END:VEVENT");
        return Result<string>.Ok(ContentLineExtensions.Join(lines));
    }
}
