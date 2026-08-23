namespace WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

/// <summary>Declares which of a validator's members must publish no rule operands in <see cref="FieldError.Params"/>.</summary>
/// <remarks>
/// <para>
/// Sensitivity is a property of the OPERATION, not of the member name — sign-up must tell the user the required password length,
/// sign-in must not, and both validate a member called <c>Password</c>. A global name-matching rule cannot express that; a
/// declaration on the validator that owns the operation can.
/// </para>
/// <para>
/// A listed member reports <see cref="FieldError.Params"/> as <see langword="null"/>. Redacting operand keys alone is not enough:
/// a password failing <c>MaximumLength</c> still reports <c>TotalLength</c>, which discloses the secret's length.
/// <see cref="FieldError.Code"/> and <see cref="FieldError.Message"/> are untouched, so the failure still renders.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class SignInCommandValidator : AbstractValidator&lt;SignInCommand&gt;, ISensitiveMembers
/// {
///     public IReadOnlySet&lt;string&gt; SensitiveMembers { get; } = new HashSet&lt;string&gt; { nameof(SignInCommand.Password) };
///
///     public SignInCommandValidator() => RuleFor(x =&gt; x.Password).NotEmpty();
/// }
/// </code>
/// </example>
public interface ISensitiveMembers
{
    /// <summary>Gets the member names whose operands never leave the server. Match the property path, e.g. <c>nameof(T.Password)</c>.</summary>
    IReadOnlySet<string> SensitiveMembers { get; }
}
