namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke.Parsers;

/// <summary>Defines parsing a migration SQL script into sequential statement texts.</summary>
public interface ISqlStatementParser
{
    /// <summary>Parses top-level semicolon boundaries while preserving quoted text and comments.</summary>
    /// <remarks>
    /// Empty or whitespace-only input produces an empty list. Empty statements are skipped.
    /// Single quotes, double quotes, dollar quotes, line comments and nested block comments are recognized.
    /// This boundary parser does not validate SQL syntax: incomplete constructs stay in the final statement
    /// and the database provider rejects invalid SQL during execution. Backslash-escaped strings are unsupported.
    /// </remarks>
    /// <param name="script">The migration script.</param>
    /// <returns>The statement texts in source order.</returns>
    IReadOnlyList<string> Parse(string script);
}
