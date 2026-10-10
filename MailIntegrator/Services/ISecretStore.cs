namespace MailIntegrator.Services;

/// <summary>
/// Stores external service credentials in an OS managed vault.
/// </summary>
/// <remarks>
/// Implementations must never persist secrets into the application database, configuration files or logs.
/// The abstraction exists so a future iteration can swap the local vault for a central secrets manager.
/// </remarks>
public interface ISecretStore
{
    /// <summary>
    /// Reads the stored credential for a service.
    /// </summary>
    /// <param name="service">The service to read.</param>
    /// <returns>The stored credential, or <see langword="null"/> when nothing is stored yet.</returns>
    StoredCredential? GetCredential(SecretService service);

    /// <summary>
    /// Creates or replaces the credential for a service.
    /// </summary>
    /// <param name="service">The service to write.</param>
    /// <param name="login">The user name component.</param>
    /// <param name="password">The secret component.</param>
    void SaveCredential(SecretService service, string login, string password);

    /// <summary>
    /// Removes the credential for a service when present.
    /// </summary>
    /// <param name="service">The service to clear.</param>
    /// <returns><see langword="true"/> when a credential was removed.</returns>
    bool DeleteCredential(SecretService service);

    /// <summary>
    /// Reports whether a credential exists for a service.
    /// </summary>
    /// <param name="service">The service to check.</param>
    /// <returns><see langword="true"/> when a credential is stored.</returns>
    bool IsConfigured(SecretService service);
}
