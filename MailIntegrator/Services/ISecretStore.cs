namespace MailIntegrator.Services;

/// <summary>
/// Stores external service credentials in an OS managed vault.
/// </summary>
/// <remarks>
/// Implementations must never persist secrets into the application database, configuration files or logs.
/// The abstraction exists so a future iteration can swap the local vault for a central secrets manager.
/// Credentials are addressed by an extensible <see cref="CredentialTarget"/>, so a new tracking provider
/// needs no change to this interface.
/// </remarks>
public interface ISecretStore
{
    /// <summary>
    /// Reads the stored credential for a target.
    /// </summary>
    /// <param name="target">The target to read.</param>
    /// <returns>The stored credential, or <see langword="null"/> when nothing is stored yet.</returns>
    StoredCredential? GetCredential(CredentialTarget target);

    /// <summary>
    /// Creates or replaces the credential for a target.
    /// </summary>
    /// <param name="target">The target to write.</param>
    /// <param name="login">The user name component.</param>
    /// <param name="password">The secret component.</param>
    void SaveCredential(CredentialTarget target, string login, string password);

    /// <summary>
    /// Removes the credential for a target when present.
    /// </summary>
    /// <param name="target">The target to clear.</param>
    /// <returns><see langword="true"/> when a credential was removed.</returns>
    bool DeleteCredential(CredentialTarget target);

    /// <summary>
    /// Reports whether a credential exists for a target.
    /// </summary>
    /// <param name="target">The target to check.</param>
    /// <returns><see langword="true"/> when a credential is stored.</returns>
    bool IsConfigured(CredentialTarget target);
}
