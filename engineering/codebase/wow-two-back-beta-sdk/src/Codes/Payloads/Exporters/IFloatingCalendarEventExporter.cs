using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Exporters;

/// <summary>Defines export of floating VEVENT fragments.</summary>
public interface IFloatingCalendarEventExporter
{
    /// <summary>Exports supplied data under the documented content-line contract.</summary>
    Result<string> Export(FloatingCalendarEventModel model);
}
