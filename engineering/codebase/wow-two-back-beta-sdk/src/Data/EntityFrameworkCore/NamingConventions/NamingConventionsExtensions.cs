namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.NamingConventions;

// Marker namespace for the SDK's naming-conventions package — referencing it pulls in `EFCore.NamingConventions`
// transitively, so consumers call `UseSnakeCaseNamingConvention` / `UseLowerCaseNamingConvention` /
// `UseCamelCaseNamingConvention` / `UseUpperSnakeCaseNamingConvention` on their `DbContextOptionsBuilder` directly
// (they live in `Microsoft.EntityFrameworkCore`).

