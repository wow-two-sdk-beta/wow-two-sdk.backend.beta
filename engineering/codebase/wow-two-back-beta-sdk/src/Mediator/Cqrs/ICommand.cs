namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>Defines a command that returns <typeparamref name="TResult"/>.</summary>
/// <typeparam name="TResult">Result type the command produces.</typeparam>
public interface ICommand<TResult> : IRequest<TResult>;

/// <summary>Defines a command.</summary>
public interface ICommand : IRequest;
