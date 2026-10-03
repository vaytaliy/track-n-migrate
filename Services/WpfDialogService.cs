using System.Windows;
using MailIntegrator.ViewModels;
using MailIntegrator.Views;

namespace MailIntegrator.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/>.
/// </summary>
public sealed class WpfDialogService : IDialogService
{
    private readonly Func<SettingsViewModel> _settingsViewModelFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WpfDialogService"/> class.
    /// </summary>
    /// <param name="settingsViewModelFactory">
    /// Creates a fresh settings view model each time the dialog is opened, so the vault is re-read.
    /// </param>
    public WpfDialogService(Func<SettingsViewModel> settingsViewModelFactory)
    {
        _settingsViewModelFactory =
            settingsViewModelFactory ?? throw new ArgumentNullException(nameof(settingsViewModelFactory));
    }

    /// <inheritdoc />
    public void ShowError(string title, string message) =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    /// <inheritdoc />
    public bool Confirm(string title, string message) =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question)
        == MessageBoxResult.OK;

    /// <inheritdoc />
    public void ShowSettings()
    {
        var window = new SettingsWindow(_settingsViewModelFactory())
        {
            Owner = Owner(),
        };

        window.ShowDialog();
    }

    /// <summary>
    /// Returns the window that should own a modal dialog.
    /// </summary>
    /// <returns>The main window when one exists.</returns>
    private static Window? Owner() => Application.Current?.MainWindow;
}
