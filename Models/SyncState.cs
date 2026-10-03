namespace MailIntegrator.Models;

/// <summary>
/// Represents the state of a synchronisation run displayed by the status strip.
/// </summary>
public enum SyncState
{
    /// <summary>No synchronisation has run recently.</summary>
    Idle,

    /// <summary>A synchronisation run is currently executing.</summary>
    InProgress,

    /// <summary>The last synchronisation run failed.</summary>
    Failed,

    /// <summary>The last synchronisation run completed successfully.</summary>
    Succeeded,
}
