using System.Globalization;
using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Maps a product build to the version stamp its applied migrations carry.</summary>
public static class MigrationVersionMapper
{
    /// <summary>
    /// Maps an assembly's version to the <c>vX.Y</c> stamp of the product version a migration shipped in — the
    /// <c>&lt;Version&gt;</c> a product declares once in <c>Directory.Build.props</c>.
    /// </summary>
    /// <param name="assembly">The product assembly, usually the host's.</param>
    /// <returns>The stamp, e.g. <c>v0.5</c>; <c>v0.0</c> when the assembly carries no version.</returns>
    public static string ToVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var version = assembly.GetName().Version ?? new Version(0, 0);
        return string.Create(CultureInfo.InvariantCulture, $"v{version.Major}.{version.Minor}");
    }
}
