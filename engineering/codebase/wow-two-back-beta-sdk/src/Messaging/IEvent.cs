using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// Defines the marker for an event contract — a fact that happened, carried by the <see cref="IEventBus"/>.
/// Only events cross the bus; commands and queries stay on the in-process mediator. Delivery topology
/// (in-memory vs broker) is a wiring choice and is deliberately absent from the contract.
/// </summary>
public interface IEvent;
