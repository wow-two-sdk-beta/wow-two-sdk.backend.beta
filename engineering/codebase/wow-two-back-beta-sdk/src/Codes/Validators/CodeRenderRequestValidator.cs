using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Validators;

/// <summary>Validates a code rendering request.</summary>
public sealed class CodeRenderRequestValidator : AbstractValidator<CodeRenderRequest>
{
    /// <summary>Creates the render request rules.</summary>
    public CodeRenderRequestValidator()
    {
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(8192);
        RuleFor(x => x.Symbology).IsInEnum();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.Style).NotNull().SetValidator(new StyleSpecValidator());
    }
}
