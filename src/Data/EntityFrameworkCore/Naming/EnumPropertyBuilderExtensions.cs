using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Naming;

/// <summary>Helpers for mapping enum properties to case-styled string columns.</summary>
public static class EnumPropertyBuilderExtensions
{
    /// <summary>Stores the enum property as a case-styled string (default <see cref="CaseStyle.Snake"/>) via <see cref="EnumCaseConverter{TEnum}"/>.</summary>
    /// <typeparam name="TEnum">The enum type stored as text.</typeparam>
    /// <param name="builder">The property builder for the enum property.</param>
    /// <param name="style">The casing emitted for enum labels. Default <see cref="CaseStyle.Snake"/>.</param>
    public static PropertyBuilder<TEnum> HasEnumStringConversion<TEnum>(
        this PropertyBuilder<TEnum> builder, CaseStyle style = CaseStyle.Snake)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.HasConversion(new EnumCaseConverter<TEnum>(style));
    }
}
