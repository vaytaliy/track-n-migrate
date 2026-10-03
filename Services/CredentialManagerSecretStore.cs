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
    private const string TargetNamePrefix = "MailIntegrator/";

    /// <inheritdoc />
    public StoredCredential? GetCredential(CredentialTarget target)
    {
        var credential = CredentialManager.ReadCredential(BuildTargetName(target));
        if (credential is null)
        {
            return null;
        }

        return new StoredCredential(credential.UserName ?? string.Empty, credential.Password ?? string.Empty);
    }

    /// <inheritdoc />
    public void SaveCredential(CredentialTarget target, string login, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentNullException.ThrowIfNull(password);

        CredentialManager.WriteCredential(BuildTargetName(target), login, password, Persistence);
    }

    /// <inheritdoc />
    public bool DeleteCredential(CredentialTarget target) =>
        CredentialManager.TryDeleteCredential(BuildTargetName(target));

    /// <inheritdoc />
    public bool IsConfigured(CredentialTarget target) => GetCredential(target) is not null;

    /// <summary>
    /// Builds the vault target name for a credential target.
    /// </summary>
    /// <param name="target">The target to name.</param>
    /// <returns>The Windows Credential Manager target name.</returns>
    /// <exception cref="ArgumentException">The target code is blank.</exception>
    private static string BuildTargetName(CredentialTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Code);
        return TargetNamePrefix + target.Code;
    }
}
