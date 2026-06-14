namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>
/// Handles a <see cref="ICommand{TResult}"/> and produces its result.
/// A naming refinement of <see cref="IRequestHandler{TRequest,TResponse}"/> — registered by the same DI scan.
/// </summary>
/// <typeparam name="TCommand">Command type handled.</typeparam>
/// <typeparam name="TResult">Result type produced.</typeparam>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;

/// <summary>
/// Handles a <see cref="ICommand"/> that produces no response.
/// A naming refinement of <see cref="IRequestHandler{TRequest}"/> — registered by the same DI scan.
/// </summary>
/// <typeparam name="TCommand">Command type handled.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;
