namespace MailIntegrator.ViewModels;

/// <summary>
/// Describes the outcome of an attempt to persist the draft grid row.
/// </summary>
public enum CommitDraftResult
{
    /// <summary>The draft row was empty, so nothing was persisted.</summary>
    NothingToCommit,

    /// <summary>The draft row was persisted and replaced with a fresh draft.</summary>
    Succeeded,

    /// <summary>The draft row failed validation and remains in place.</summary>
    Blocked,
}
