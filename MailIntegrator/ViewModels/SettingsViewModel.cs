using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailIntegrator.Services;

namespace MailIntegrator.ViewModels;

/// <summary>
/// View model for the credential settings dialog.
/// </summary>
/// <remarks>
/// Secrets are written straight to Windows Credential Manager; nothing is persisted in the application
/// database or in any configuration file.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// Backing field for <see cref="StatusMessage"/>.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="secretStore">The vault that stores the credentials.</param>
    public SettingsViewModel(ISecretStore secretStore)
    {
        ArgumentNullException.ThrowIfNull(secretStore);

        Services = SecretServices.All
            .Select(service => new ServiceCredentialsViewModel(service, secretStore))
            .ToList();
    }

    /// <summary>
    /// Raised when the dialog should close; the payload is the dialog result.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>
    /// Gets the credential editors, one per supported service.
    /// </summary>
    public IReadOnlyList<ServiceCredentialsViewModel> Services { get; }

    /// <summary>
    /// Validates the input of every service and writes the credentials to the vault.
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        var incomplete = Services.Where(service => !service.IsInputValid()).ToList();
        if (incomplete.Count > 0)
        {
            StatusMessage =
                "Заполните логин и пароль для: " + string.Join(", ", incomplete.Select(service => service.DisplayName));
            return;
        }

        foreach (var service in Services)
        {
            service.Save();
        }

        StatusMessage = "Учётные данные сохранены в Windows Credential Manager.";
    }

    /// <summary>
    /// Removes the stored credential of a single service.
    /// </summary>
    /// <param name="service">The service editor whose credential should be removed.</param>
    [RelayCommand]
    private void ClearService(ServiceCredentialsViewModel? service)
    {
        if (service is null)
        {
            return;
        }

        service.Clear();
        StatusMessage = $"Учётные данные для «{service.DisplayName}» удалены.";
    }

    /// <summary>
    /// Closes the dialog without saving.
    /// </summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, false);
}
