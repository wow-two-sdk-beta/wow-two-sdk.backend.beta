using System.Collections.Concurrent;
using System.Reflection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Mediator.Result;

/// <summary>Creates <see cref="AppResult{TSuccess}"/> failures for an arbitrary response type via cached reflection.</summary>
public static class AppResultFactory
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> FailFactories = new();

    /// <summary>Attempts to build an <c>AppResult&lt;TSuccess&gt;.Failure</c> for <typeparamref name="TResponse"/> when it is an <see cref="AppResult{TSuccess}"/>.</summary>
    /// <typeparam name="TResponse">The mediator response type.</typeparam>
    /// <param name="error">The error to carry in the failure.</param>
    /// <param name="failure">The constructed failure, when <typeparamref name="TResponse"/> is an <see cref="AppResult{TSuccess}"/>.</param>
    public static bool TryCreateFailure<TResponse>(AppError error, out TResponse failure)
    {
        ArgumentNullException.ThrowIfNull(error);

        var factory = FailFactories.GetOrAdd(typeof(TResponse), ResolveFailFactory);

        if (factory is null)
        {
            failure = default!;

            return false;
        }

        failure = (TResponse)factory.Invoke(null, [error, null])!;

        return true;
    }

    private static MethodInfo? ResolveFailFactory(Type responseType)
    {
        var appResultType = FindAppResultType(responseType);

        if (appResultType is null)
        {
            return null;
        }

        return appResultType.GetMethod(
            nameof(AppResult<object>.Fail),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(AppError), typeof(IAppFailureContext)]);
    }

    private static Type? FindAppResultType(Type responseType)
    {
        var current = responseType;

        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AppResult<>))
            {
                return current;
            }

            current = current.BaseType;
        }

        return null;
    }
}
