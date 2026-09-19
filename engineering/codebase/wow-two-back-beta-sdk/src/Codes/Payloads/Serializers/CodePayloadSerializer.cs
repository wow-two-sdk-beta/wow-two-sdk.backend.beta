using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Payloads.Serializers;

/// <summary>Serializes typed code payloads into scanner URI and WIFI formats.</summary>
public sealed class CodePayloadSerializer : ICodePayloadSerializer
{
    /// <summary>Serializes WIFI credentials without trimming the SSID or password.</summary>
    public Result<string> Serialize(WifiPayloadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Enum.IsDefined(model.Authentication)) return Invalid("Authentication", "Unsupported WIFI authentication.");
        var token = model.Authentication switch { WifiAuthentication.Wpa => "WPA", WifiAuthentication.Wep => "WEP", _ => "nopass" };
        var password = model.Authentication == WifiAuthentication.None ? "" : "P:" + EscapeWifi(model.Password) + ";";
        return Result<string>.Ok($"WIFI:T:{token};S:{EscapeWifi(model.Ssid)};{password}{(model.Hidden ? "H:true;" : "")};");
    }

    /// <summary>Serializes an RFC 6068 mailto draft with subject and body headers only.</summary>
    public Result<string> Serialize(MailPayloadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var recipient = Clean(model.Recipient);
        var subject = Clean(model.Subject);
        if (recipient.Any(char.IsControl) || subject.Contains('\r') || subject.Contains('\n'))
            return Invalid("Recipient", "Recipient and subject must not contain line breaks or control characters.");
        var query = new List<string>();
        if (subject.Length > 0) query.Add("subject=" + Uri.EscapeDataString(subject));
        var body = Clean(model.Body).ReplaceLineEndings("\r\n");
        if (body.Length > 0) query.Add("body=" + Uri.EscapeDataString(body));
        var address = Uri.EscapeDataString(recipient).Replace("%40", "@", StringComparison.Ordinal);
        return Result<string>.Ok("mailto:" + address + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }

    /// <summary>Serializes a scanner SMSTO payload.</summary>
    public Result<string> Serialize(SmsPayloadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var body = Clean(model.Message);
        return Result<string>.Ok("SMSTO:" + Phone(model.Phone) + (body.Length > 0 ? ":" + body : ""));
    }

    /// <summary>Serializes a telephone URI.</summary>
    public Result<string> Serialize(PhonePayloadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Result<string>.Ok("tel:" + Phone(model.Phone));
    }

    /// <summary>Serializes an invariant WGS84 geo URI.</summary>
    public Result<string> Serialize(GeoPayloadModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!double.IsFinite(model.Latitude) || !double.IsFinite(model.Longitude)
            || model.Latitude is < -90 or > 90 || model.Longitude is < -180 or > 180)
            return Invalid("Coordinates", "Use finite WGS84 latitude and longitude.");
        return Result<string>.Ok(string.Create(CultureInfo.InvariantCulture, $"geo:{model.Latitude},{model.Longitude}"));
    }

    private static string Phone(string? value) => Uri.EscapeDataString(Clean(value)).Replace("%2B", "+", StringComparison.Ordinal);
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string EscapeWifi(string? value)
    {
        var output = new StringBuilder();
        foreach (var c in value ?? string.Empty)
        {
            if (c is '\\' or ';' or ',' or ':' or '"') output.Append('\\');
            output.Append(c);
        }
        return output.ToString();
    }

    private static Result<string> Invalid(string property, string message) => Result<string>.Fail(ValidationError.From(
        [new FieldError { Property = property, Message = message, Code = "CodePayloadFormat" }]));
}
