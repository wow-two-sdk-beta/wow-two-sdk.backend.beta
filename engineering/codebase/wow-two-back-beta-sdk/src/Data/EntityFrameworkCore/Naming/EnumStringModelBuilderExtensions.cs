using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WoW.Two.Sdk.Backend.Beta.Foundation.Naming;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Naming;

/// <summary>Model-wide enum-to-string conversion — the bulk counterpart to the per-property <see cref="EnumPropertyBuilderExtensions.HasEnumStringConversion{TEnum}"/>.</summary>
public static class EnumStringModelBuilderExtensions
{
    /// <summary>Stores every enum property in the model (nullable and non-nullable) as a case-styled string (default <see cref="CaseStyle.Snake"/>) via <see cref="EnumCaseConverter{TEnum}"/>; call from <c>OnModelCreating</c> after the model's properties are mapped.</summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="style">The casing emitted for enum labels. Default <see cref="CaseStyle.Snake"/>.</param>
    public static ModelBuilder ApplyEnumStringConversions(this ModelBuilder modelBuilder, CaseStyle style = CaseStyle.Snake)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                var enumType = Nullable.GetUnderlyingType(clrType) ?? clrType;
                if (enumType.IsEnum)
                    property.SetValueConverter(CreateConverter(enumType, style));
            }
        }

        return modelBuilder;
    }

    /// <summary>Builds a closed <see cref="EnumCaseConverter{TEnum}"/> for the runtime enum type.</summary>
    /// <param name="enumType">The (non-nullable) enum CLR type to build a converter for.</param>
    /// <param name="style">The casing emitted for enum labels.</param>
    private static ValueConverter CreateConverter(Type enumType, CaseStyle style)
    {
        var converterType = typeof(EnumCaseConverter<>).MakeGenericType(enumType);
        return (ValueConverter)Activator.CreateInstance(converterType, style)!;
    }
}
