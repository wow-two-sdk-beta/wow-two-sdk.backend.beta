using System.Globalization;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Localization;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ErrorTranslation;

/// <summary>Template filling: placeholders, formats, CLDR plurals with exact matches, nesting and pseudo-localization.</summary>
public sealed class MessageTemplateMapperTests
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru");
    private const string Orders = "{Count, plural, =0 {нет заказов} one {# заказ} few {# заказа} many {# заказов} other {# заказа}}";

    [Theory]
    [InlineData(0, "нет заказов")]
    [InlineData(1, "1 заказ")]
    [InlineData(3, "3 заказа")]
    [InlineData(5, "5 заказов")]
    [InlineData(11, "11 заказов")]
    [InlineData(21, "21 заказ")]
    [InlineData(22, "22 заказа")]
    [InlineData(112, "112 заказов")]
    public void RussianPlurals_FollowTheCldrRules(int count, string expected)
    {
        MessageTemplateMapper.TryFormat(Orders, Args(("Count", count)), Russian, out var message).Should().BeTrue();
        message.Should().Be(expected);
    }

    [Theory]
    [InlineData("en", 1, "one")]
    [InlineData("en", 2, "other")]
    [InlineData("uz", 1, "one")]
    [InlineData("uz", 7, "other")]
    [InlineData("ru", 1.5, "other")]
    [InlineData("ja", 1, "other")]
    public void PluralCategory_CoversTheSupportedLanguages(string culture, double number, string expected)
        => MessageTemplateMapper.PluralCategory((decimal)number, CultureInfo.GetCultureInfo(culture)).Should().Be(expected);

    [Fact]
    public void Branches_FillNestedPlaceholdersAndFormats()
    {
        const string template = "{Count, plural, one {# item for {Name}} other {# items for {Name}}} — total {Total:N2}";

        MessageTemplateMapper.TryFormat(template, Args(("Count", 2), ("Name", "Ali"), ("Total", 1234.5m)), CultureInfo.GetCultureInfo("en"), out var message)
            .Should().BeTrue();

        message.Should().Be("2 items for Ali — total 1,234.50");
    }

    [Fact]
    public void MissingOrNonNumericArguments_Fail()
    {
        MessageTemplateMapper.TryFormat("{Missing}", Args(), Russian, out _).Should().BeFalse();
        MessageTemplateMapper.TryFormat(Orders, Args(("Count", "many")), Russian, out _).Should().BeFalse();
        MessageTemplateMapper.TryFormat("no placeholders", null, Russian, out var plain).Should().BeTrue();
        plain.Should().Be("no placeholders");
    }

    [Fact]
    public void Pseudo_AccentsLettersAndBracketsTheText()
        => MessageTemplateMapper.Pseudo("Not found 404").Should().Be("[!! Ñöţ ƒöûñđ 404 !!]");

    private static Dictionary<string, object?> Args(params (string Name, object? Value)[] values)
        => values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
}
