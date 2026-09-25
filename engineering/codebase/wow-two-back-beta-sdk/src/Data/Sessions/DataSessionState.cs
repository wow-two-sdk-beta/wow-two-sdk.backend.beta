namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Defines the lifetime states of one scoped transaction owner.</summary>
public enum DataSessionState
{
    /// <summary>No transaction has started.</summary>
    Idle,
    /// <summary>An explicit unit owns the transaction.</summary>
    Active,
    /// <summary>The database confirmed the commit.</summary>
    Committed,
    /// <summary>The root unit was rolled back.</summary>
    RolledBack,
    /// <summary>A transaction operation failed; dispose the scope.</summary>
    Faulted,
    /// <summary>The session was disposed.</summary>
    Disposed
}
