using System.Reflection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Specs;

/// <summary>
/// Registers the <see cref="EntitySpec"/> of each entity at composition, keyed by entity type; mappers read it to build
/// their own configuration (EF Core model, Dapper SQL). Later registrations of one type replace earlier ones.
/// </summary>
public sealed class EntitySpecRegistry
{
    private readonly Dictionary<Type, EntitySpec> _specs = [];

    /// <summary>Every registered spec.</summary>
    public IReadOnlyCollection<EntitySpec> Specs => _specs.Values;

    /// <summary>The spec of <paramref name="entityType"/>, or null when none is registered.</summary>
    /// <param name="entityType">The entity type.</param>
    public EntitySpec? Find(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _specs.GetValueOrDefault(entityType);
    }

    /// <summary>Registers the spec <paramref name="configure"/> builds.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="configure">Configures the builder.</param>
    public EntitySpecRegistry Add<TEntity>(Action<EntitySpecBuilder<TEntity>> configure)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new EntitySpecBuilder<TEntity>();
        configure(builder);
        _specs[typeof(TEntity)] = builder.Build();
        return this;
    }

    /// <summary>Registers the spec <paramref name="configuration"/> builds.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="configuration">The entity's spec configuration.</param>
    public EntitySpecRegistry Add<TEntity>(IEntitySpecConfiguration<TEntity> configuration)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Add<TEntity>(configuration.Configure);
    }

    /// <summary>Registers every <see cref="IEntitySpecConfiguration{TEntity}"/> with a parameterless constructor in <paramref name="assemblies"/>.</summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public EntitySpecRegistry AddFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var add = typeof(EntitySpecRegistry).GetMethods()
            .Single(method => method.Name == nameof(Add) && method.GetParameters()[0].ParameterType.IsGenericType
                && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEntitySpecConfiguration<>));

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition || type.GetConstructor(Type.EmptyTypes) is null)
                continue;

            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntitySpecConfiguration<>)))
                add.MakeGenericMethod(contract.GetGenericArguments()[0]).Invoke(this, [Activator.CreateInstance(type)]);
        }

        return this;
    }
}
