using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Options;

/// <summary>Registers an options, settings or module-options record through one recipe: fill it, validate it, project it.</summary>
/// <remarks>
///   - validation runs at boot, so a bad value fails the host rather than the first request
///   - the record is projected, so a consumer takes <c>T</c> and never <c>IOptions&lt;T&gt;</c>
///   - <c>required</c> enforces nothing here: the pipeline builds through <c>Activator.CreateInstance</c>
/// </remarks>
public static class OptionsRegistrationExtensions
{
    /// <summary>Registers <typeparamref name="TOptions"/> filled by a delegate.</summary>
    /// <typeparam name="TOptions">The options record.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Fills the record; null keeps every default.</param>
    /// <param name="validate">States the rules the filled record must satisfy.</param>
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        Action<TOptions>? configure,
        Action<OptionsBuilder<TOptions>> validate)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validate);

        var builder = services.AddOptions<TOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        return Seal(services, builder, validate);
    }

    /// <summary>Registers <typeparamref name="TSettings"/> bound from <paramref name="section"/>.</summary>
    /// <typeparam name="TSettings">The settings record.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration the section is read from.</param>
    /// <param name="section">The section name the record binds to.</param>
    /// <param name="validate">States the rules the bound record must satisfy.</param>
    public static IServiceCollection AddValidatedSettings<TSettings>(
        this IServiceCollection services,
        IConfiguration configuration,
        string section,
        Action<OptionsBuilder<TSettings>> validate)
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(validate);

        var builder = services.AddOptions<TSettings>().Bind(configuration.GetSection(section));

        return Seal(services, builder, validate);
    }

    /// <summary>
    /// Registers a module's <typeparamref name="TOptions"/> from three layers, later ones winning: the record's defaults,
    /// <paramref name="configure"/> from code, then the host configuration section <paramref name="sectionPath"/>. Host
    /// configuration comes last so an environment can change a module without a rebuild; the section reloads live
    /// through <see cref="IOptionsMonitor{TOptions}"/>. Safe to call once per caller: every call adds its
    /// <paramref name="configure"/>, the first also binds the section and states the rules.
    /// </summary>
    /// <typeparam name="TOptions">The module's options record.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="sectionPath">The host configuration section, such as <c>Validation</c> or <c>Identity:TwoFactor</c>.</param>
    /// <param name="configure">Code configuration; null keeps the defaults.</param>
    /// <param name="validate">States the rules the final record must satisfy; applied once.</param>
    public static IServiceCollection AddModuleOptions<TOptions>(
        this IServiceCollection services,
        string sectionPath,
        Action<TOptions>? configure,
        Action<OptionsBuilder<TOptions>>? validate = null)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

        var builder = services.AddOptions<TOptions>();
        if (configure is not null)
            builder.Configure(configure);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IOptionsChangeTokenSource<TOptions>)))
            return services;

        builder.PostConfigure<IServiceProvider>((options, provider) =>
            provider.GetService<IConfiguration>()?.GetSection(sectionPath).Bind(options));
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(provider =>
            new ConfigurationChangeTokenSource<TOptions>(
                (IConfiguration?)provider.GetService<IConfiguration>()?.GetSection(sectionPath) ?? new ConfigurationBuilder().Build()));
        validate?.Invoke(builder);
        builder.ValidateDataAnnotations().ValidateOnStart();
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<TOptions>>().Value);
        return services;
    }

    /// <summary>Applies the caller's rules, the data annotations, the boot check, and the projection.</summary>
    /// <typeparam name="T">The record being registered.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="builder">The builder the record was filled or bound through.</param>
    /// <param name="validate">States the rules the record must satisfy.</param>
    private static IServiceCollection Seal<T>(
        IServiceCollection services, OptionsBuilder<T> builder, Action<OptionsBuilder<T>> validate)
        where T : class
    {
        validate(builder);
        builder.ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<T>>().Value);

        return services;
    }
}
