namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;

/// <summary>
/// Defines one section of a user's personal-data export, such as the product's orders or uploads. Register each with
/// <c>TryAddEnumerable</c>; every registered exporter runs on an export, in registration order.
/// </summary>
/// <typeparam name="TUser">The user entity.</typeparam>
public interface IPersonalDataExporter<in TUser>
{
    /// <summary>Gets the section name in the export, such as <c>orders</c>; unique across exporters.</summary>
    string Section { get; }

    /// <summary>Exports the user's data of this section as a JSON-serializable value; null leaves the section empty.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<object?> ExportAsync(TUser user, CancellationToken cancellationToken = default);
}
