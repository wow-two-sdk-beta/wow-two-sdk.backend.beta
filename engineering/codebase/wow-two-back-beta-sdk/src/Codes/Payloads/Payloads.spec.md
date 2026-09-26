# Code payload contracts

Typed serialization covers WIFI, mailto, SMSTO, tel and geo; document export covers vCard 3.0 and floating VEVENT.
Product unions, redirect policy, delivery and business validation stay with the caller. Null models are programmer errors.
Optional null/blank fields are omitted; SSIDs/passwords and literal text retain whitespace. Phone/SMS/mail fields retain existing trimming.

- WIFI preserves WPA/WEP/open tokens and escaping of backslash, semicolon, comma, colon and quote. Unknown authentication fails.
- mailto uses UTF-8 percent encoding, including spaces as `%20` and literal plus as `%2B`. Recipients cannot inject query headers.
  Body line breaks become CRLF; subject line breaks are rejected. Only subject and body headers are emitted.
- SMSTO retains the colon-separated scanner format; recipient delimiters are percent encoded.
- geo uses invariant finite WGS84 coordinates within latitude/longitude bounds.
- vCard emits the supplied contact as v3: N, FN, optional ORG/TITLE/TEL/EMAIL/URL/ADR/NOTE. Text is escaped;
  CR/LF cannot inject properties. CRLF separates lines; folding caps physical lines at 75 UTF-8 octets without splitting a scalar.
- VEVENT export explicitly takes timezone-free LocalDateTime. No conversion or product timezone choice is implied.
  UTC/TZID/full VCALENDAR, UID and delivery semantics remain the deferred calendar vector.
  A supplied end must follow start; an absent end omits DTEND without inferring a duration.
- Typed encoders return Result<string> for invalid format data. They do not validate product entitlement or field requirements.

Protocol corrections to the product corpus: mailto space/plus encoding, body CRLF, vCard/VEVENT CRLF and safe line folding.
Other valid corpus outputs preserve their represented values. Text needs no wrapper; URL/mobile-app routing stays product-local.

References: [mailto RFC 6068](https://www.rfc-editor.org/rfc/rfc6068),
[vCard 3 RFC 2426](https://www.rfc-editor.org/rfc/rfc2426),
[iCalendar RFC 5545](https://www.rfc-editor.org/rfc/rfc5545).

Registration: `services.AddCodePayloads()` provides `ICodePayloadSerializer`, `IVCardExporter` and
`IFloatingCalendarEventExporter`. Each is replaceable at composition; serializers/exporters do not send anything.
Floating events use positive ISO calendar years and second precision. Product timezone selection remains deferred.
