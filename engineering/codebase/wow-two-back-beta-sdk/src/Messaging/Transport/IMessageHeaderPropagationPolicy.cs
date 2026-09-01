namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Decides which headers flow from a consumed message onto a message published while handling it. The seam exists
/// because "carry the tenant id forward, drop the caller's debug flag" is an application decision, not a transport one.
/// </summary>
/// <remarks>
///   - propagates only inside <see cref="EventContext{TEvent}"/>
///   - replace the registered policy to change what flows
///   - defaults to <see cref="MessageHeaderPropagationPolicy.Default"/>
/// </remarks>
public interface IMessageHeaderPropagationPolicy
{
    /// <summary>
    /// True when a header of this name should be copied from the consumed message onto the outgoing one. Reserved
    /// (<see cref="MessageHeaderConstants.ReservedPrefix"/>) keys are blocked by the caller regardless of the answer, so an
    /// implementation never has to guard the control namespace itself.
    /// </summary>
    /// <param name="key">The inbound header key.</param>
    bool ShouldPropagate(string key);
}
