using System.Collections.ObjectModel;

namespace MailIntegrator.Services;

/// <summary>
/// The shared list of handled failures of the current synchronisation pass.
/// </summary>
/// <remarks>
/// <para>
/// One instance is created per application and shared by every pass and by the footer, so a new concrete
/// implementation only has to report its failures and never has to know where they are displayed.
/// </para>
/// <para>
/// The owning pass calls <see cref="BeginPass"/> before it starts, which empties the list, and then reports
/// every failure it handled. The list is observable, so the footer updates while the pass is still running.
/// </para>
/// </remarks>
public interface IUserErrorLog
{
    /// <summary>
    /// Gets the failures of the current pass, in the order they were reported.
    /// </summary>
    ReadOnlyObservableCollection<UserError> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the current pass reported at least one failure.
    /// </summary>
    bool HasErrors { get; }

    /// <summary>
    /// Empties the list at the start of a pass, so the operator only ever sees failures of the last action.
    /// </summary>
    void BeginPass();

    /// <summary>
    /// Appends one handled failure.
    /// </summary>
    /// <param name="error">The failure to display.</param>
    void Report(UserError error);
}
