using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Naming;

/// <summary>
/// EF Core value converter that persists an enum as a case-styled string (default <see cref="CaseStyle.Snake"/>),
/// reversibly. Reads are case-insensitive on the label; writes emit the configured style.
/// </summary>
/// <remarks>
/// The reverse map is built from the enum's own members (see <see cref="EnumNameConverter{TEnum}"/>),
/// so values round-trip exactly — no underscore-stripping heuristic, unlike a naive
/// <c>ToString().ToSnakeCase()</c> + <c>Enum.Parse</c> pair.
/// </remarks>
/// <typeparam name="TEnum">The enum type stored as text.</typeparam>
public sealed class EnumCaseConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>Creates a converter emitting <paramref name="style"/> labels (default <see cref="CaseStyle.Snake"/>).</summary>
    public EnumCaseConverter(CaseStyle style = CaseStyle.Snake)
        : base(
            value => EnumNameConverter<TEnum>.ToLabel(value, style),
            label => EnumNameConverter<TEnum>.Parse(label, style))
    {
    }
}
