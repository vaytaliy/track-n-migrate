using MailIntegrator.Services;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Records dialog interactions so view model behaviour can be asserted without any UI.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>
    /// Gets or sets the answer returned by <see cref="Confirm"/>.
    /// </summary>
    public bool ConfirmResult { get; set; }

    /// <summary>
    /// Gets the titles of the errors shown so far.
    /// </summary>
    public List<string> ShownErrors { get; } = [];

    /// <summary>
    /// Gets the confirmation prompts shown so far.
    /// </summary>
    public List<string> Confirmations { get; } = [];

    /// <summary>
    /// Gets the number of times the settings dialog was requested.
    /// </summary>
    public int SettingsShownCount { get; private set; }

    /// <inheritdoc />
    public void ShowError(string title, string message) => ShownErrors.Add($"{title}: {message}");

    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        Confirmations.Add($"{title}: {message}");
        return ConfirmResult;
    }

    /// <inheritdoc />
    public void ShowSettings() => SettingsShownCount++;
}
