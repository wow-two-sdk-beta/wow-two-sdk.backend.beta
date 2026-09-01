
namespace WoW.Two.Sdk.Backend.Beta.Foundation.Errors;

/// <summary>Defines the seam that translates any caught exception into an <see cref="AppError"/> — the single mapper the mediator behavior and global handlers depend on.</summary>
public interface IExceptionMapper
{
    /// <summary>Maps <paramref name="exception"/> to an <see cref="AppError"/>, always returning a value (falling back to <see cref="AppErrorType.Unexpected"/>).</summary>
    /// <param name="exception">The caught exception.</param>
    AppError Map(Exception exception);
}
