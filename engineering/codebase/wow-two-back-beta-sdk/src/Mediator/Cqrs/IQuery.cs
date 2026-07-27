namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>Defines a query that returns <typeparamref name="TResult"/>.</summary>
/// <typeparam name="TResult">Result type the query produces.</typeparam>
public interface IQuery<TResult> : IRequest<TResult>;
