namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>Defines a handler for the <typeparamref name="TQuery"/> query.</summary>
/// <typeparam name="TQuery">Query type handled.</typeparam>
/// <typeparam name="TResult">Result type produced.</typeparam>
public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
