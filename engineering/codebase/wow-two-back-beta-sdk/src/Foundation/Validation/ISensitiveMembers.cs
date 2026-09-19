namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Defines behavior that declares which of a validator's members must publish no rule operands in <see cref="FieldError.Params"/>.</summary>
/// <remarks>
///   - declare per validator — sensitivity belongs to the operation, not the member name
///   - a listed member reports <see cref="FieldError.Params"/> as <see langword="null"/>, not a filtered set
///   - <see cref="FieldError.Code"/> and <see cref="FieldError.Message"/> are untouched — the failure still renders
/// </remarks>
public interface ISensitiveMembers
{
    /// <summary>Gets the member names whose operands never leave the server. Match the property path, e.g. <c>nameof(T.Password)</c>.</summary>
    IReadOnlySet<string> SensitiveMembers { get; }
}
