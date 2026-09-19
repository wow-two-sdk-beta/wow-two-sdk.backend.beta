namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors.Mappers;

/// <summary>Defines mapping an <see cref="AppErrorType"/> into its <see cref="ErrorNature"/>.</summary>
public interface IErrorNatureMapper
{
    /// <summary>Maps <paramref name="type"/> to its descriptive nature.</summary>
    /// <param name="type">The failure kind.</param>
    /// <returns>The descriptive error nature.</returns>
    ErrorNature Map(AppErrorType type);
}
