using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Validators;

internal static class CodeRenderValidationExtensions
{
    private static readonly FluentValidationAdapter<StyleSpec> StyleValidator = new FluentValidationAdapter<StyleSpec>([new StyleSpecValidator()]);
    private static readonly FluentValidationAdapter<CodeRenderRequest> RequestValidator = new FluentValidationAdapter<CodeRenderRequest>([new CodeRenderRequestValidator()]);

    internal static void ValidateForRendering(this StyleSpec style) => StyleValidator.ValidateAndThrow(style);
    internal static void ValidateForRendering(this CodeRenderRequest request) => RequestValidator.ValidateAndThrow(request);

    internal static ValidationException Invalid(string property, string message) => new(ValidationError.From(
        [new FieldError { Property = property, Message = message, Code = "CodeRenderingInput" }]));
}
