using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>Code-level setup for the EF test-DB tier — the single place that selects the provider. Mutate <see cref="Current"/> once (e.g. a module initializer) to switch the whole suite.</summary>
public sealed class TestSetupOptions
{
    /// <summary>The relational provider the test-DB fixtures use; defaults to Postgres (the fidelity baseline). Set to <see cref="DatabaseProvider.Sqlite"/> to flip the suite to in-memory SQLite.</summary>
    public DatabaseProvider Database { get; set; } = DatabaseProvider.Postgres;

    /// <summary>The shared options instance the test-DB fixtures read.</summary>
    public static TestSetupOptions Current { get; } = new();
}
