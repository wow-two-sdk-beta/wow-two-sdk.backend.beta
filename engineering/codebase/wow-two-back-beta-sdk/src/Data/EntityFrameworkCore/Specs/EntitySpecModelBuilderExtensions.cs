using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WoW.Two.Sdk.Backend.Beta.Data.Specs;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Specs;

/// <summary>
/// Maps <see cref="EntitySpec"/>s into an EF Core model: table, key, columns, indexes, concurrency tokens and soft-delete
/// filters. A token the provider cannot express (<c>xmin</c> off PostgreSQL, <c>rowversion</c> off SQL Server) follows
/// <see cref="UnsupportedSpecMode"/>.
/// </summary>
public static class EntitySpecModelBuilderExtensions
{
    /// <summary>The annotation marking a <see cref="ConcurrencyTokenKind.Stamp"/> property, replaced on every save by <see cref="AppDbContextBase"/>.</summary>
    public const string ConcurrencyStampAnnotation = "WoW2:ConcurrencyStamp";

    private const string MapperName = "EntityFrameworkCore";

    /// <summary>Applies the registry's specs to the entity types already in the model (from a <c>DbSet</c> or a configuration).</summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="registry">The specs.</param>
    /// <param name="providerName">The context's <c>Database.ProviderName</c>; null applies every token as declared.</param>
    /// <param name="unsupported">What to do with a feature the provider cannot express.</param>
    /// <param name="onSkipped">Receives each skipped feature under <see cref="UnsupportedSpecMode.Skip"/>.</param>
    /// <exception cref="UnsupportedSpecException">A feature is unsupported and <paramref name="unsupported"/> is <see cref="UnsupportedSpecMode.Throw"/>.</exception>
    public static ModelBuilder ApplyEntitySpecs(
        this ModelBuilder modelBuilder,
        EntitySpecRegistry registry,
        string? providerName = null,
        UnsupportedSpecMode unsupported = UnsupportedSpecMode.Throw,
        Action<string>? onSkipped = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(registry);

        var skipped = new List<string>();
        foreach (var spec in registry.Specs)
        {
            if (modelBuilder.Model.FindEntityType(spec.EntityType) is null)
                continue;

            Apply(modelBuilder.Entity(spec.EntityType), spec, providerName, skipped);
        }

        if (skipped.Count > 0 && unsupported == UnsupportedSpecMode.Throw)
            throw new UnsupportedSpecException(MapperName, skipped);

        foreach (var feature in skipped)
            onSkipped?.Invoke(feature);

        return modelBuilder;
    }

    private static void Apply(EntityTypeBuilder entity, EntitySpec spec, string? providerName, List<string> skipped)
    {
        if (spec.Table is not null)
            entity.ToTable(spec.Table, spec.Schema);
        if (spec.Key.Count > 0)
            entity.HasKey([.. spec.Key]);

        foreach (var property in spec.Properties.Values)
        {
            if (property.IsIgnored)
            {
                entity.Ignore(property.Name);
                continue;
            }

            ApplyProperty(entity.Property(property.Name), property);
        }

        foreach (var index in spec.Indexes)
        {
            var builder = entity.HasIndex([.. index.Properties]).IsUnique(index.IsUnique);
            if (index.Name is not null)
                builder.HasDatabaseName(index.Name);
            if (index.Filter is not null)
                builder.HasFilter(index.Filter);
        }

        if (spec.Concurrency is { } token)
            ApplyToken(entity, spec, token, providerName, skipped);
        if (spec.SoftDeleteProperty is { } softDelete)
            entity.HasQueryFilter(NotDeleted(spec.EntityType, softDelete));
    }

    private static void ApplyProperty(PropertyBuilder builder, PropertySpec property)
    {
        if (property.Column is not null)
            builder.HasColumnName(property.Column);
        if (property.IsRequired is { } required)
            builder.IsRequired(required);
        if (property.MaxLength is { } maxLength)
            builder.HasMaxLength(maxLength);
        if (property.Precision is { } precision)
        {
            if (property.Scale is { } scale)
                builder.HasPrecision(precision, scale);
            else
                builder.HasPrecision(precision);
        }

        if (property.IsUnicode is { } unicode)
            builder.IsUnicode(unicode);
        if (property.DefaultValueSql is not null)
            builder.HasDefaultValueSql(property.DefaultValueSql);
        if (property.ComputedSql is not null)
            builder.HasComputedColumnSql(property.ComputedSql);

        _ = property.Generation switch
        {
            ValueGenerationKind.OnAdd => builder.ValueGeneratedOnAdd(),
            ValueGenerationKind.OnAddOrUpdate => builder.ValueGeneratedOnAddOrUpdate(),
            _ => builder,
        };
    }

    private static void ApplyToken(EntityTypeBuilder entity, EntitySpec spec, ConcurrencySpec token, string? providerName, List<string> skipped)
    {
        var property = entity.Property(token.Property);
        switch (token.Kind)
        {
            case ConcurrencyTokenKind.Counter:
                property.IsConcurrencyToken();
                break;
            case ConcurrencyTokenKind.Stamp:
                property.IsConcurrencyToken().HasAnnotation(ConcurrencyStampAnnotation, true);
                break;
            case ConcurrencyTokenKind.RowVersion when Is(providerName, "SqlServer"):
                property.IsRowVersion();
                break;
            case ConcurrencyTokenKind.Xmin when Is(providerName, "Npgsql"):
                property.HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
                break;
            default:
                skipped.Add($"{spec.EntityType.Name}.{token.Property} ({token.Kind} concurrency on {providerName})");
                break;
        }
    }

    /// <summary>Whether <paramref name="providerName"/> is unknown or names <paramref name="provider"/>.</summary>
    private static bool Is(string? providerName, string provider)
        => providerName is null || providerName.Contains(provider, StringComparison.OrdinalIgnoreCase);

    private static LambdaExpression NotDeleted(Type entityType, string property)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var isDeleted = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(bool)], parameter, Expression.Constant(property));
        return Expression.Lambda(Expression.Not(isDeleted), parameter);
    }
}
