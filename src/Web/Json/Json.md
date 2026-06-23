# Web — Json

Controller JSON presets — `AddApiDefaults` registers no controllers, so these are the seam for the JSON wire contract.

- `AddJsonStringEnums(this IMvcBuilder)` — adds `JsonStringEnumConverter`, leaving other options untouched (the targeted form).
- `AddControllersWithSdkJson(this IServiceCollection)` — `AddControllers()` + the SDK `JsonOptionsPresets.Default` preset (camelCase, null-ignoring, NodaTime, relaxed escaping) + string enums.
