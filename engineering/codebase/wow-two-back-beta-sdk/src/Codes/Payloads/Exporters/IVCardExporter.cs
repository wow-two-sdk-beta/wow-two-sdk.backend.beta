using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Exporters;

/// <summary>Defines export of vCard 3.0 contact documents.</summary>
public interface IVCardExporter
{
    /// <summary>Exports supplied data under the documented content-line contract.</summary>
    string Export(ContactCardModel model);
}
