namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>
/// Handles a <see cref="IQuery{TResult}"/> and produces its result.
/// A naming refinement of <see cref="IRequestHandler{TRequest,TResponse}"/> — registered by the same DI scan.
/// </summary>
/// <typeparam name="TQuery">Query type handled.</typeparam>
/// <typeparam name="TResult">Result type produced.</typeparam>
public interface IQueryHandler<in TQuery, TResult> : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>;
