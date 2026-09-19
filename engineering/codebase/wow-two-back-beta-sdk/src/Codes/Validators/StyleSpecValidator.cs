using System.Buffers;
using FluentValidation;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Validators;

/// <summary>Validates the style of a rendered code.</summary>
public sealed class StyleSpecValidator : AbstractValidator<StyleSpec>
{
    private readonly InlineImageValidator _images = new();
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");
    /// <summary>Creates the rendering style rules.</summary>
    public StyleSpecValidator()
    {
        RuleFor(x => x.SchemaVersion).Equal(StyleSpec.CurrentSchemaVersion);
        RuleFor(x => x.ForegroundColor).Must(IsColor).WithMessage("Use a six-digit hexadecimal RGB color.");
        RuleFor(x => x.BackgroundColor).Must(IsColor).WithMessage("Use a six-digit hexadecimal RGB color.");
        RuleFor(x => x.EccLevel).IsInEnum();
        RuleFor(x => x.ModuleShape).IsInEnum();
        RuleFor(x => x.FinderShape).IsInEnum();
        RuleFor(x => x.FinderDotShape).IsInEnum();
        RuleFor(x => x.QuietZoneModules).InclusiveBetween(0, 32);
        When(x => x.Logo is not null, () =>
        {
            RuleFor(x => x.Logo!.SizeRatio).Must(IsRatio).WithMessage("Use a finite ratio greater than zero and at most one.");
            RuleFor(x => x.Logo!.DataUrl).Must(_images.IsValid).WithMessage("Use a bounded inline PNG or JPEG image.");
        });
        When(x => x.Emoji is not null, () =>
        {
            RuleFor(x => x.Emoji!.SizeRatio).Must(IsRatio);
            RuleFor(x => x.Emoji!.Glyph).NotEmpty().MaximumLength(64).Must(IsXmlText);
        });
        RuleFor(x => x.Gradient).Custom((gradient, context) =>
        {
            if (gradient is null) return;
            if (gradient is not LinearGradientSpec and not RadialGradientSpec)
                context.AddFailure("Unsupported gradient.");
            if (gradient is LinearGradientSpec linear && (!double.IsFinite(linear.Angle) || linear.Angle is < -360 or > 360))
                context.AddFailure("Gradient.Angle", "Use an angle from -360 to 360 degrees.");
            if (gradient is RadialGradientSpec radial && !IsRatio(radial.Radius))
                context.AddFailure("Gradient.Radius", "Use a finite ratio greater than zero and at most one.");
            if (gradient.Stops is null || gradient.Stops.Count > 32)
            {
                context.AddFailure("Gradient.Stops", "Use at most 32 stops.");
                return;
            }
            var previous = 0d;
            for (var i = 0; i < gradient.Stops.Count; i++)
            {
                var stop = gradient.Stops[i];
                if (stop is null || !double.IsFinite(stop.Offset) || stop.Offset < previous || stop.Offset > 1 || !IsColor(stop.Color))
                    context.AddFailure($"Gradient.Stops[{i}]", "Use ordered offsets from zero to one and hexadecimal RGB colors.");
                if (stop is not null) previous = stop.Offset;
            }
        });
    }

    private static bool IsColor(string? value) => value is { Length: 7 }
        && value[0] == '#' && value.AsSpan(1).IndexOfAnyExcept(HexDigits) < 0;

    private static bool IsRatio(double value) => double.IsFinite(value) && value is > 0 and <= 1;

    private static bool IsXmlText(string? value)
    {
        if (value is null) return false;
        try { System.Xml.XmlConvert.VerifyXmlChars(value); return true; }
        catch (System.Xml.XmlException) { return false; }
    }
}
