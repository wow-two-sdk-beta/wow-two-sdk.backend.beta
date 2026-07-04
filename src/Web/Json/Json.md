# Web — Json

Controller JSON presets — `AddApiDefaults` registers no controllers, so these are the seam for the JSON wire contract.

- `AddJsonStringEnums(this IMvcBuilder)` — adds `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`, leaving other options untouched (the targeted form). Enums travel as **camelCase** strings — the wire enum contract (`conventions/development/backend/presentation/serialization.md`).
- `AddControllersWithSdkJson(this IServiceCollection)` — `AddControllers()` + the SDK `JsonOptionsPresets.Default` preset (camelCase, null-ignoring, NodaTime, relaxed escaping) + camelCase string enums.
