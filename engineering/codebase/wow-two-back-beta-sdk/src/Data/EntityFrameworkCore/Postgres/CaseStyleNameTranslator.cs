using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>Npgsql name translator that routes type and member names through the SDK casing engine, so enum labels match the rest of the SDK's casing instead of Npgsql's built-in snake translator.</summary>
/// <param name="style">The casing applied to translated names.</param>
public sealed class CaseStyleNameTranslator(CaseStyle style) : INpgsqlNameTranslator
{
    /// <inheritdoc />
    public string TranslateTypeName(string clrName) => CaseConverter.ToCase(clrName, style);

    /// <inheritdoc />
    public string TranslateMemberName(string clrName) => CaseConverter.ToCase(clrName, style);
}
