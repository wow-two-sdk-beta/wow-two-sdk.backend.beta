using Xunit;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

/// <summary>Proves the pre-emit normalizer rules: quiet-zone floor, logo→H, stylised-module ECC floor (≥ Q).</summary>
public class StyleSpecMapperTests
{
    [Theory]
    [InlineData(EccLevel.L)]
    [InlineData(EccLevel.M)]
    public void Stylised_module_shape_bumps_low_ecc_up_to_Q(EccLevel low)
    {
        // A non-square body shrinks the per-module decode margin → ECC floored to Q.
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Dots,
            EccLevel = low,
        });

        Assert.Equal(EccLevel.Q, normalized.EccLevel);
    }

    [Theory]
    [InlineData(EccLevel.Q)]
    [InlineData(EccLevel.H)]
    public void Stylised_module_shape_never_lowers_already_high_ecc(EccLevel high)
    {
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with
        {
            ModuleShape = ModuleShape.VerticalBars,
            EccLevel = high,
        });

        Assert.Equal(high, normalized.EccLevel);
    }

    [Fact]
    public void Square_module_shape_keeps_callers_low_ecc()
    {
        // The byte-parity default must not be forced upward — square keeps whatever the caller chose.
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Square,
            EccLevel = EccLevel.L,
        });

        Assert.Equal(EccLevel.L, normalized.EccLevel);
    }

    [Fact]
    public void Logo_forces_H_even_with_a_stylised_body()
    {
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Dots,
            EccLevel = EccLevel.L,
            Logo = new LogoSpec { DataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", SizeRatio = 0.2 },
        });

        Assert.Equal(EccLevel.H, normalized.EccLevel);
    }

    [Fact]
    public void Finder_only_styling_does_not_bump_ecc()
    {
        // Eyes are drawn complete from geometry, so styling them alone doesn't reduce the data margin → no bump.
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Square,
            FinderShape = FinderShape.Circle,
            FinderDotShape = FinderDotShape.Circle,
            EccLevel = EccLevel.L,
        });

        Assert.Equal(EccLevel.L, normalized.EccLevel);
    }

    [Fact]
    public void Quiet_zone_below_floor_is_clamped()
    {
        var normalized = StyleSpecMapper.Normalize(StyleSpec.Default with { QuietZoneModules = 1 });

        Assert.Equal(QuietZoneConstants.MinModules, normalized.QuietZoneModules);
    }
}
