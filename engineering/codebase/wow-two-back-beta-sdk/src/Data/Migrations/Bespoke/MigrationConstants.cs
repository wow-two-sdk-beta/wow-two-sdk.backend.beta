using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Holds the file names, directives and folder-name pattern the migrator depends on.</summary>
public static partial class MigrationConstants
{
    /// <summary>Holds the Apply script file name inside each migration folder.</summary>
    public const string ApplyFileName = "Apply.sql";

    /// <summary>Holds the Rollback script file name inside each migration folder.</summary>
    public const string RollbackFileName = "Rollback.sql";

    /// <summary>Holds the directive (as a leading SQL comment) that makes an Apply script run outside a transaction.</summary>
    public const string NoTransactionDirective = "-- @no-transaction";

    /// <summary>Holds the whitespace-stripped form of <see cref="NoTransactionDirective"/> used for header matching.</summary>
    public const string NoTransactionDirectiveCompact = "--@no-transaction";

    /// <summary>Holds the reserved subfolder holding unpromoted dev drafts — never read as a numbered migration.</summary>
    public const string DevFolderName = "Dev";

    /// <summary>Matches a migration folder name of the form <c>NNN-name</c> (three-digit ordinal, then text).</summary>
    [GeneratedRegex(@"^(\d{3})-(.+)$")]
    public static partial Regex FolderPattern();
}
