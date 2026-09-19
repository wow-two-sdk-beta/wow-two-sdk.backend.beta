# Web — Json

Controller JSON presets — `AddApiDefaults` registers no controllers, so these are the seam for the JSON wire contract.

- `AddJsonStringEnums(this IMvcBuilder)` — adds `JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)`, leaving other options untouched.
- `AddControllersWithSdkJson(this IServiceCollection)` — `AddControllers()` plus the complete `JsonOptionsConstants.Default` wire preset.
- Enum values travel as camelCase strings; numeric input, undefined values and unknown flag bits fail.
- `TimeSpan` values travel as ISO 8601 duration strings such as `P2DT1S`.
- Properties and dictionary keys use camelCase; null properties are omitted on write.
