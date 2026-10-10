using CommunityToolkit.Mvvm.ComponentModel;
using MailIntegrator.Services;

namespace MailIntegrator.ViewModels;

/// <summary>
/// Holds the editable credentials of a single external service for the settings dialog.
/// </summary>
public sealed partial class ServiceCredentialsViewModel : ObservableObject
{
    private readonly ISecretStore _secretStore;

    /// <summary>
    /// Backing field for <see cref="Login"/>.
    /// </summary>
    [ObservableProperty]
    private string _login = string.Empty;

    /// <summary>
    /// Backing field for <see cref="Password"/>.
    /// </summary>
    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>
    /// Backing field for <see cref="IsConfigured"/>.
    /// </summary>
    [ObservableProperty]
    private bool _isConfigured;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceCredentialsViewModel"/> class.
    /// </summary>
    /// <param name="service">The service these credentials belong to.</param>
    /// <param name="secretStore">The vault used to read and write the credential.</param>
    public ServiceCredentialsViewModel(SecretService service, ISecretStore secretStore)
    {
        Service = service;
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        Reload();
    }

    /// <summary>
    /// Gets the service these credentials belong to.
    /// </summary>
    public SecretService Service { get; }

    /// <summary>
    /// Gets the human readable service name shown in the dialog.
    /// </summary>
    public string DisplayName => Service.ToDisplayName();

    /// <summary>
    /// Gets a value indicating whether the credential is currently stored in the vault.
    /// </summary>
    public string ConfiguredCaption => IsConfigured ? "Сохранено" : "Не задано";

    /// <summary>
    /// Re-reads the credential from the vault into the editable properties.
    /// </summary>
    public void Reload()
    {
        var credential = _secretStore.GetCredential(Service);
        Login = credential?.Login ?? string.Empty;
        Password = credential?.Password ?? string.Empty;
        IsConfigured = credential is not null;
    }

    /// <summary>
    /// Persists the current values, or removes the credential when the login is empty.
    /// </summary>
    /// <returns><see langword="true"/> when a credential is stored after the operation.</returns>
    public bool Save()
    {
        if (string.IsNullOrWhiteSpace(Login))
        {
            Clear();
            return false;
        }

        _secretStore.SaveCredential(Service, Login.Trim(), Password);
        IsConfigured = true;
        return true;
    }

    /// <summary>
    /// Removes the stored credential for this service.
    /// </summary>
    public void Clear()
    {
        _secretStore.DeleteCredential(Service);
        Login = string.Empty;
        Password = string.Empty;
        IsConfigured = false;
    }

    /// <summary>
    /// Gets a value indicating whether the current input is complete enough to be stored.
    /// </summary>
    /// <returns><see langword="true"/> when both components are present or both are empty.</returns>
    public bool IsInputValid() =>
        string.IsNullOrWhiteSpace(Login) == string.IsNullOrWhiteSpace(Password);

    /// <summary>
    /// Notifies the configured caption when the flag changes.
    /// </summary>
    /// <param name="value">The new flag value.</param>
    partial void OnIsConfiguredChanged(bool value) => OnPropertyChanged(nameof(ConfiguredCaption));
}
