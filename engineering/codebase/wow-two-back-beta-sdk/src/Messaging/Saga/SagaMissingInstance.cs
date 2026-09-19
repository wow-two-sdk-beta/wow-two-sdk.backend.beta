using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Refers to what to do with an event whose saga instance does not exist and which no <c>Initially</c> clause would create.</summary>
public enum SagaMissingInstance
{
    /// <summary>Drop the event. The default: an event correlating to nothing usually belongs to a flow that already finalized.</summary>
    Ignore = 0,

    /// <summary>Throw, so the message takes the normal retry / dead-letter path. Use where a missing instance means the events arrived out of order.</summary>
    Fault = 1,
}
