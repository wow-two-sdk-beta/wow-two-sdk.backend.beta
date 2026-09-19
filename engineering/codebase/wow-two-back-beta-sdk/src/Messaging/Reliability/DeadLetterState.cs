namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Refers to where a dead-lettered message sits in the administration lifecycle.</summary>
public enum DeadLetterState
{
    /// <summary>Awaiting triage — visible to <see cref="IDeadLetterAdmin.BrowseAsync"/> and eligible for redrive. Every record starts here.</summary>
    DeadLettered,

    /// <summary>
    /// Held back by an operator. Still stored and still readable, but hidden from a browse that does not ask for it and
    /// refused by redrive — the state for a message that must not go back on the wire until someone decides otherwise.
    /// </summary>
    Quarantined,
}
