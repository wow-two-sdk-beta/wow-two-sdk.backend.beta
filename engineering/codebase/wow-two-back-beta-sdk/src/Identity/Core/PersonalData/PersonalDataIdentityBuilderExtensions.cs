using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.PersonalData;

/// <summary>Opts personal-data export and account deletion into an identity registration.</summary>
public static class PersonalDataIdentityBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="UserPersonalDataService{TUser,TKey}"/>; call after <c>AddEntityFrameworkStores</c>. Products add
    /// <see cref="IPersonalDataExporter{TUser}"/> sections and <see cref="IAccountDeletionHandler{TUser}"/> steps with
    /// <c>TryAddEnumerable</c>. The account API then maps <c>manage/personal-data</c> and <c>manage/delete-account</c>.
    /// </summary>
    /// <typeparam name="TUser">The user entity.</typeparam>
    /// <typeparam name="TKey">The primary-key type.</typeparam>
    /// <param name="builder">The identity builder.</param>
    /// <exception cref="InvalidOperationException">The EF stores are not registered yet.</exception>
    public static IdentityBuilder<TUser, TKey> AddPersonalData<TUser, TKey>(this IdentityBuilder<TUser, TKey> builder)
        where TUser : IdentityUser<TKey>
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(builder);
        var context = builder.ContextFactory ?? throw new InvalidOperationException("Call AddEntityFrameworkStores before AddPersonalData.");
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddScoped(serviceProvider => new UserPersonalDataService<TUser, TKey>(
            context(serviceProvider),
            serviceProvider.GetServices<IPersonalDataExporter<TUser>>(),
            serviceProvider.GetServices<IAccountDeletionHandler<TUser>>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        return builder;
    }
}
