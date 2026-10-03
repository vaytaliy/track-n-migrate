namespace MailIntegrator.Services;

/// <summary>
/// Abstracts modal window interactions so view models stay free of WPF types.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a modal error message.
    /// </summary>
    /// <param name="title">The dialog caption.</param>
    /// <param name="message">The message body.</param>
    void ShowError(string title, string message);

    /// <summary>
    /// Asks the operator to confirm an action.
    /// </summary>
    /// <param name="title">The dialog caption.</param>
    /// <param name="message">The question body.</param>
    /// <returns><see langword="true"/> when the operator confirms.</returns>
    bool Confirm(string title, string message);

    /// <summary>
    /// Opens the credential settings dialog.
    /// </summary>
    void ShowSettings();
}
