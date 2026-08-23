using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Naming;

/// <summary>Persists an enum as a case-styled string (default <see cref="CaseStyle.Snake"/>) reversibly.</summary>
/// <typeparam name="TEnum">The enum type stored as text.</typeparam>
public sealed class EnumCaseMapper<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>Creates a Snake-case converter, the parameterless overload EF Core needs to build the converter model-level.</summary>
    public EnumCaseMapper()
        : this(CaseStyle.Snake)
    {
    }

    /// <summary>Creates a converter emitting <paramref name="style"/> labels (default <see cref="CaseStyle.Snake"/>).</summary>
    /// <param name="style">The casing emitted for enum labels. Default <see cref="CaseStyle.Snake"/>.</param>
    public EnumCaseMapper(CaseStyle style = CaseStyle.Snake)
        : base(
            value => EnumNameMapper<TEnum>.ToLabel(value, style),
            label => EnumNameMapper<TEnum>.Parse(label, style))
    {
    }
}
