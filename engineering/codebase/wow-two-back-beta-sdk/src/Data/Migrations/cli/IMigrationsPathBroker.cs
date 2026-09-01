using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Cli;

/// <summary>Defines the lookup that locates the on-disk migrations directory for the filesystem source.</summary>
/// <remarks>A repo that keeps migrations elsewhere swaps the implementation instead of passing <c>--sql-dir</c> every run.</remarks>
internal interface IMigrationsPathBroker
{
    /// <summary>Locates the migrations directory, or fails when no candidate exists.</summary>
    Result<string> Locate();
}
