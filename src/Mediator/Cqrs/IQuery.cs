namespace WoW.Two.Sdk.Backend.Beta.Mediator.Cqrs;

/// <summary>
/// Marks a query — a read-only operation that returns a result of type <typeparamref name="TResult"/>.
/// A CQRS-flavored refinement of <see cref="IRequest{TResponse}"/>; dispatched through the same pipeline.
/// </summary>
/// <typeparam name="TResult">Result type the query produces.</typeparam>
public interface IQuery<TResult> : IRequest<TResult>;
