namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>
/// Marks a command — a write operation that returns a result of type <typeparamref name="TResult"/>.
/// A CQRS-flavored refinement of <see cref="IRequest{TResponse}"/>; dispatched through the same pipeline.
/// </summary>
/// <typeparam name="TResult">Result type the command produces.</typeparam>
public interface ICommand<TResult> : IRequest<TResult>;

/// <summary>
/// Marks a command with no return value — a write operation that produces only <see cref="Unit"/>.
/// A CQRS-flavored refinement of <see cref="IRequest"/>.
/// </summary>
public interface ICommand : IRequest;
