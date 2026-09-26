using System.Globalization;
using System.Text;
using NodaTime;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Exporters;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Serializers;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

public sealed class CodePayloadTests
{
    private readonly CodePayloadSerializer _serializer = new CodePayloadSerializer();
    private readonly VCardExporter _cards = new VCardExporter();
    private readonly FloatingCalendarEventExporter _events = new FloatingCalendarEventExporter();

    [Fact]
    public void Wifi_PreservesCorpusWhitespaceAndEscapesDelimiters()
    {
        Assert.Equal("WIFI:T:WPA;S:Cafe;P:p@ss;;", _serializer.Serialize(new WifiPayloadModel { Ssid = "Cafe", Password = "p@ss" }).ValueOrThrow());
        Assert.Equal("""WIFI:T:WPA;S:My\;Net;P:a\"b\,c;;""", _serializer.Serialize(new WifiPayloadModel { Ssid = "My;Net", Password = "a\"b,c" }).ValueOrThrow());
        Assert.Equal("WIFI:T:nopass;S: Open ;;", _serializer.Serialize(new WifiPayloadModel { Ssid = " Open ", Password = "ignored", Authentication = WifiAuthentication.None }).ValueOrThrow());
        Assert.Equal("WIFI:T:WEP;S:Hid;P: x ;H:true;;", _serializer.Serialize(new WifiPayloadModel { Ssid = "Hid", Password = " x ", Authentication = WifiAuthentication.Wep, Hidden = true }).ValueOrThrow());
        Assert.False(_serializer.Serialize(new WifiPayloadModel { Authentication = (WifiAuthentication)99 }).IsSuccess);
    }

    [Fact]
    public void Mailto_UsesPercentEncodingWithoutHeaderInjection()
    {
        Assert.Equal("mailto:a@b.com", _serializer.Serialize(new MailPayloadModel { Recipient = "a@b.com" }).ValueOrThrow());
        Assert.Equal("mailto:a%2Bb@c.com?subject=Hi%20%26%20bye&body=line%20one%0D%0A%2B%C3%A9",
            _serializer.Serialize(new MailPayloadModel { Recipient = "a+b@c.com", Subject = "Hi & bye", Body = "line one\n+é" }).ValueOrThrow());
        Assert.Equal("mailto:a@b.com%3Fbcc%3Devil@c.com", _serializer.Serialize(new MailPayloadModel { Recipient = "a@b.com?bcc=evil@c.com" }).ValueOrThrow());
        Assert.False(_serializer.Serialize(new MailPayloadModel { Subject = "ok\r\nBcc:evil@c.com" }).IsSuccess);
    }

    [Fact]
    public void PhoneSmsAndGeo_PreserveCorpusAndInvariantCoordinates()
    {
        Assert.Equal("tel:+15550100", _serializer.Serialize(new PhonePayloadModel { Phone = "+15550100" }).ValueOrThrow());
        Assert.Equal("SMSTO:+15550100", _serializer.Serialize(new SmsPayloadModel { Phone = "+15550100" }).ValueOrThrow());
        Assert.Equal("SMSTO:+15550100:hey", _serializer.Serialize(new SmsPayloadModel { Phone = "+15550100", Message = "hey" }).ValueOrThrow());
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("geo:41.31,69.24", _serializer.Serialize(new GeoPayloadModel { Latitude = 41.31, Longitude = 69.24 }).ValueOrThrow());
        }
        finally { CultureInfo.CurrentCulture = prior; }
        Assert.False(_serializer.Serialize(new GeoPayloadModel { Latitude = double.NaN }).IsSuccess);
        Assert.False(_serializer.Serialize(new GeoPayloadModel { Longitude = 181 }).IsSuccess);
    }

    [Fact]
    public void VCard_PreservesContactFieldsEscapesNewlinesAndFoldsUnicode()
    {
        var note = string.Concat(Enumerable.Repeat("é🎉", 50));
        var output = _cards.Export(new ContactCardModel
        {
            FirstName = "Ada", LastName = "Lovelace", Organization = "Babbage, Inc", Phone = "+15550100",
            Note = note + "\rINJECT:bad",
        });
        var unfolded = output.Replace("\r\n ", "", StringComparison.Ordinal);
        Assert.Contains("N:Lovelace;Ada;;;\r\nFN:Ada Lovelace", unfolded);
        Assert.Contains("ORG:Babbage\\, Inc", unfolded);
        Assert.Contains("TEL;TYPE=CELL:+15550100", unfolded);
        Assert.Contains("NOTE:" + note + "\\nINJECT:bad", unfolded);
        Assert.DoesNotContain("EMAIL:", unfolded);
        Assert.EndsWith("END:VCARD", output);
        Assert.All(output.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));
    }

    [Fact]
    public void Calendar_DeclaresFloatingTimesAndRejectsReversedRanges()
    {
        var model = new FloatingCalendarEventModel
        {
            Title = "Launch", Start = new LocalDateTime(2026, 7, 1, 18, 30), End = new LocalDateTime(2026, 7, 1, 19, 0), Location = "HQ",
        };
        Assert.Equal("BEGIN:VEVENT\r\nSUMMARY:Launch\r\nDTSTART:20260701T183000\r\nDTEND:20260701T190000\r\nLOCATION:HQ\r\nEND:VEVENT",
            _events.Export(model).ValueOrThrow());
        Assert.False(_events.Export(model with { End = model.Start }).IsSuccess);
    }

    [Fact]
    public void Calendar_OmitsAbsentEndWithoutInventingDuration()
    {
        var model = new FloatingCalendarEventModel
        {
            Title = "Launch", Start = new LocalDateTime(2026, 7, 1, 18, 30),
        };
        Assert.Equal("BEGIN:VEVENT\r\nSUMMARY:Launch\r\nDTSTART:20260701T183000\r\nEND:VEVENT",
            _events.Export(model).ValueOrThrow());
        Assert.False(_events.Export(model with { End = new LocalDateTime(2026, 7, 1, 18, 29) }).IsSuccess);
        Assert.False(_events.Export(model with { End = new LocalDateTime(2026, 7, 1, 19, 0).WithCalendar(CalendarSystem.Julian) }).IsSuccess);
        Assert.False(_events.Export(model with { Start = new LocalDateTime(0, 1, 1, 0, 0) }).IsSuccess);
    }
}
