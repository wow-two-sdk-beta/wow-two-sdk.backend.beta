
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines a single exception-translation rule — recognizes a family of exceptions and maps them to an <see cref="AppError"/>, returning <see langword="null"/> to defer to the next rule.</summary>
public interface IExceptionMappingRule
{
    /// <summary>Maps <paramref name="exception"/> to an <see cref="AppError"/>, or returns <see langword="null"/> when this rule does not recognize it.</summary>
    /// <param name="exception">The caught exception.</param>
    AppError? TryMap(Exception exception);
}
