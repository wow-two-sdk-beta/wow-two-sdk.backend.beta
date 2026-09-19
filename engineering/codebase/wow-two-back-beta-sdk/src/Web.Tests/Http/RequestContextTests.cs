using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Mappers;
using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;
using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Parsers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Http;

public sealed class RequestContextTests
{
    private readonly AcceptLanguageParser _languages = new AcceptLanguageParser();

    [Theory]
    [InlineData(null, UserAgentDevice.Unknown)]
    [InlineData("Mozilla iPhone", UserAgentDevice.Ios)]
    [InlineData("Linux Android", UserAgentDevice.Android)]
    [InlineData("Mozilla Windows", UserAgentDevice.Desktop)]
    [InlineData("Mozilla iPhone GoogleBOT", UserAgentDevice.Bot)]
    [InlineData("custom", UserAgentDevice.Unknown)]
    public void UserAgent_PreservesClassificationOrder(string? value, UserAgentDevice expected)
        => Assert.Equal(expected, UserAgentDeviceMapper.Map(value));

    [Fact]
    public void Language_OrdersWeightsRetainingExclusionsWildcardAndTies()
    {
        var values = _languages.Parse("en;q=0.5, FR-ca, de;q=0.5, *;q=0.1, es;q=0").ValueOrThrow();
        Assert.Equal((string[])["fr-ca", "en", "de", "*", "es"], values.Select(x => x.Range));
        Assert.Equal((double[])[1d, 0.5, 0.5, 0.1, 0], values.Select(x => x.Quality));
        Assert.Empty(_languages.Parse(null).ValueOrThrow());
    }

    [Theory]
    [InlineData("en;q=2")]
    [InlineData("en;q=bogus")]
    [InlineData("en_US")]
    [InlineData("en;q=0,en;q=1")]
    [InlineData("123")]
    public void Language_RejectsMalformedOrAmbiguousPreferences(string value)
        => Assert.False(_languages.Parse(value).IsSuccess);
}
