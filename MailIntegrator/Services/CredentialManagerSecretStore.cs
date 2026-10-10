using Meziantou.Framework.Win32;

namespace MailIntegrator.Services;

/// <summary>
/// <see cref="ISecretStore"/> implementation backed by Windows Credential Manager.
/// </summary>
/// <remarks>
/// <para>
/// Credential Manager is the operating system's purpose-built vault for user secrets: entries are
/// encrypted with DPAPI under the current user, isolated per Windows account, roamed only when the
/// enterprise policy asks for it, and visible to administrators under "Windows Credentials".
/// </para>
/// <para>
/// <see cref="CredentialPersistence.LocalMachine"/> keeps a credential on this machine for this user,
/// which is the expected behaviour for a desktop client. Switching to
/// <see cref="CredentialPersistence.Enterprise"/> would let it roam with a domain profile.
/// </para>
/// </remarks>
public sealed class CredentialManagerSecretStore : ISecretStore
{
    private const CredentialPersistence Persistence = CredentialPersistence.LocalMachine;

    /// <inheritdoc />
    public StoredCredential? GetCredential(SecretService service)
    {
        var credential = CredentialManager.ReadCredential(service.ToTargetName());
        if (credential is null)
        {
            return null;
        }

        return new StoredCredential(credential.UserName ?? string.Empty, credential.Password ?? string.Empty);
    }

    /// <inheritdoc />
    public void SaveCredential(SecretService service, string login, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentNullException.ThrowIfNull(password);

        CredentialManager.WriteCredential(service.ToTargetName(), login, password, Persistence);
    }

    /// <inheritdoc />
    public bool DeleteCredential(SecretService service) =>
        CredentialManager.TryDeleteCredential(service.ToTargetName());

    /// <inheritdoc />
    public bool IsConfigured(SecretService service) => GetCredential(service) is not null;
}
