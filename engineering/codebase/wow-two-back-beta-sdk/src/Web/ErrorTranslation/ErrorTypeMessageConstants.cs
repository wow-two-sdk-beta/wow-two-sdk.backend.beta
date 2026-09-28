using System.Collections.Frozen;

namespace WoW.Two.Sdk.Backend.Beta.Web.ErrorTranslation;

/// <summary>Holds the SDK's built-in top-level message per <c>AppErrorType</c>, by neutral culture; host catalogs override them.</summary>
internal static class ErrorTypeMessageConstants
{
    /// <summary>Culture → error type name → message.</summary>
    internal static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Messages =
        new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Unexpected"] = "Произошла непредвиденная ошибка.",
                ["Validation"] = "Одно или несколько полей заполнены неверно.",
                ["NotFound"] = "Запрошенный ресурс не найден.",
                ["Conflict"] = "Запрос конфликтует с текущим состоянием ресурса.",
                ["Unauthorized"] = "Требуется вход в систему.",
                ["Forbidden"] = "У вас нет прав на это действие.",
                ["TooManyRequests"] = "Слишком много запросов. Повторите попытку позже.",
                ["DbTimeout"] = "Сервер не успел обработать запрос. Повторите попытку.",
                ["OperationTimeout"] = "Операция не завершилась вовремя. Повторите попытку.",
                ["ExternalUnauthorized"] = "Внешний сервис отклонил учётные данные.",
                ["ExternalUnavailable"] = "Внешний сервис временно недоступен.",
                ["FileNotFound"] = "Файл не найден.",
                ["SerializationFailed"] = "Не удалось прочитать данные запроса.",
                ["DataIntegrity"] = "Данные нарушают ограничения целостности.",
                ["BusinessRule"] = "Операция нарушает бизнес-правило.",
                ["PaymentRequired"] = "Для этого действия требуется оплата.",
                ["Gone"] = "Ресурс больше не доступен.",
                ["Canceled"] = "Операция была отменена.",
            }.ToFrozenDictionary(StringComparer.Ordinal),
            ["uz"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Unexpected"] = "Kutilmagan xatolik yuz berdi.",
                ["Validation"] = "Bir yoki bir nechta maydon notoʻgʻri toʻldirilgan.",
                ["NotFound"] = "Soʻralgan resurs topilmadi.",
                ["Conflict"] = "Soʻrov resursning joriy holatiga zid.",
                ["Unauthorized"] = "Tizimga kirish talab qilinadi.",
                ["Forbidden"] = "Bu amal uchun sizda ruxsat yoʻq.",
                ["TooManyRequests"] = "Soʻrovlar juda koʻp. Keyinroq qayta urinib koʻring.",
                ["DbTimeout"] = "Server soʻrovni oʻz vaqtida bajara olmadi. Qayta urinib koʻring.",
                ["OperationTimeout"] = "Amal oʻz vaqtida yakunlanmadi. Qayta urinib koʻring.",
                ["ExternalUnauthorized"] = "Tashqi xizmat hisob maʼlumotlarini rad etdi.",
                ["ExternalUnavailable"] = "Tashqi xizmat vaqtincha ishlamayapti.",
                ["FileNotFound"] = "Fayl topilmadi.",
                ["SerializationFailed"] = "Soʻrov maʼlumotlarini oʻqib boʻlmadi.",
                ["DataIntegrity"] = "Maʼlumotlar yaxlitlik cheklovlarini buzadi.",
                ["BusinessRule"] = "Amal biznes qoidasini buzadi.",
                ["PaymentRequired"] = "Bu amal uchun toʻlov talab qilinadi.",
                ["Gone"] = "Resurs endi mavjud emas.",
                ["Canceled"] = "Amal bekor qilindi.",
            }.ToFrozenDictionary(StringComparer.Ordinal),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
