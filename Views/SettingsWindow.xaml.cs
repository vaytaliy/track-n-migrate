using System.Windows;
using System.Windows.Controls;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Views;

/// <summary>
/// Modal dialog used to capture the external service credentials.
/// </summary>
/// <remarks>
/// <see cref="PasswordBox"/> deliberately does not expose a bindable password property, so the
/// passwords are pushed to and pulled from the view model by the two handlers below. This keeps the
/// secret out of the binding engine and out of any dependency property store.
/// </remarks>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the dialog.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    /// <summary>
    /// Seeds a password box with the value loaded from the vault.
    /// </summary>
    /// <param name="sender">The password box.</param>
    /// <param name="e">The event arguments.</param>
    private void OnPasswordBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { Tag: ServiceCredentialsViewModel service } passwordBox)
        {
            passwordBox.Password = service.Password;
        }
    }

    /// <summary>
    /// Copies an edited password back into the view model.
    /// </summary>
    /// <param name="sender">The password box.</param>
    /// <param name="e">The event arguments.</param>
    private void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { Tag: ServiceCredentialsViewModel service } passwordBox)
        {
            service.Password = passwordBox.Password;
        }
    }

    /// <summary>
    /// Closes the dialog when the view model asks for it.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="result">The dialog result.</param>
    private void OnCloseRequested(object? sender, bool result) => DialogResult = result;

    /// <summary>
    /// Detaches the view model event so the dialog can be collected.
    /// </summary>
    /// <param name="sender">The dialog.</param>
    /// <param name="e">The event arguments.</param>
    private void OnClosed(object? sender, EventArgs e) => _viewModel.CloseRequested -= OnCloseRequested;
}
