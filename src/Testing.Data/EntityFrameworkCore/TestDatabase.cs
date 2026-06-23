using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>The relational provider the EF test-DB tier runs on, read once from the <c>WOW2_TEST_DB</c> environment variable (default Postgres — closest to production).</summary>
public static class TestDatabase
{
    /// <summary>The configured provider — <see cref="DatabaseProvider.Sqlite"/> when <c>WOW2_TEST_DB=sqlite</c>, otherwise <see cref="DatabaseProvider.Postgres"/>.</summary>
    public static DatabaseProvider Provider { get; } =
        string.Equals(Environment.GetEnvironmentVariable("WOW2_TEST_DB"), "sqlite", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.Sqlite
            : DatabaseProvider.Postgres;
}
