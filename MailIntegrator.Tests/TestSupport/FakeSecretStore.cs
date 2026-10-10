using MailIntegrator.Services;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// In-memory stand-in for the Windows Credential Manager vault, keyed by credential target code.
/// </summary>
public sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, StoredCredential> _credentials = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the number of credentials currently held.
    /// </summary>
    public int Count => _credentials.Count;

    /// <inheritdoc />
    public StoredCredential? GetCredential(CredentialTarget target) =>
        _credentials.TryGetValue(target.Code, out var credential) ? credential : null;

    /// <inheritdoc />
    public void SaveCredential(CredentialTarget target, string login, string password) =>
        _credentials[target.Code] = new StoredCredential(login, password);

    /// <inheritdoc />
    public bool DeleteCredential(CredentialTarget target) => _credentials.Remove(target.Code);

    /// <inheritdoc />
    public bool IsConfigured(CredentialTarget target) => _credentials.ContainsKey(target.Code);
}
