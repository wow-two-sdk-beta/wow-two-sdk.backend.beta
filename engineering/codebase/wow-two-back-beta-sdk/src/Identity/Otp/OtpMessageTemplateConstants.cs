namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Holds the built-in one-time-code wording for <c>en</c>, <c>ru</c> and <c>uz</c>; configured templates override it.</summary>
internal static class OtpMessageTemplateConstants
{
    /// <summary>Culture → key → template.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Templates =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = "{code} is your {app} verification code. It expires in {minutes, plural, one {# minute} other {# minutes}}. Do not share it with anyone.",
                ["subject"] = "Your {app} verification code",
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = "{code} — ваш код подтверждения {app}. Код действует {minutes, plural, one {# минуту} few {# минуты} many {# минут} other {# минуты}}. Никому его не сообщайте.",
                ["subject"] = "Ваш код подтверждения {app}",
            },
            ["uz"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = "{code} — {app} tasdiqlash kodingiz. Kod {minutes} daqiqa amal qiladi. Uni hech kimga bermang.",
                ["subject"] = "{app} tasdiqlash kodingiz",
            },
        };
}
