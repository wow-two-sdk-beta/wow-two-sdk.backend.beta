namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>Defines a handler for the <typeparamref name="TCommand"/> command.</summary>
/// <typeparam name="TCommand">Command type handled.</typeparam>
/// <typeparam name="TResult">Result type produced.</typeparam>
public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;

/// <summary>Defines a handler for the <typeparamref name="TCommand"/> command.</summary>
/// <typeparam name="TCommand">Command type handled.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;
