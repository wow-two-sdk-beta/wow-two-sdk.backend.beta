namespace WoW.Two.Sdk.Backend.Beta.Mediator;

/// <summary>Marker for a request that produces a response of type <typeparamref name="TResponse"/>.</summary>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface IRequest<TResponse> : IBaseRequest;

/// <summary>Marker for a request that produces no response (returns <see cref="Unit"/>).</summary>
public interface IRequest : IRequest<Unit>;
