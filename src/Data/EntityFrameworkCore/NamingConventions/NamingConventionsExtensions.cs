namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.NamingConventions;

/// <summary>
/// Marker namespace for the SDK's naming-conventions package. Adding this PackageReference
/// pulls in <c>EFCore.NamingConventions</c> transitively; consumers call
/// <c>UseSnakeCaseNamingConvention</c> / <c>UseLowerCaseNamingConvention</c> /
/// <c>UseCamelCaseNamingConvention</c> / <c>UseUpperSnakeCaseNamingConvention</c> on their
/// <c>DbContextOptionsBuilder</c> directly (they live in <c>Microsoft.EntityFrameworkCore</c>).
/// </summary>
internal static class NamingConventionsMarker;
