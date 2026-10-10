namespace MailIntegrator.Data;

/// <summary>
/// Persistence operations for non-secret application settings.
/// </summary>
/// <remarks>
/// Credentials must never be stored through this abstraction; they belong in Windows Credential Manager.
/// </remarks>
public interface IAppSettingsRepository
{
    /// <summary>
    /// Reads a single setting value.
    /// </summary>
    /// <param name="key">The setting identifier.</param>
    /// <returns>The stored value, or <see langword="null"/> when the key is absent.</returns>
    string? Get(string key);

    /// <summary>
    /// Inserts or replaces a setting value and stamps the update instant.
    /// </summary>
    /// <param name="key">The setting identifier.</param>
    /// <param name="value">The value to store, or <see langword="null"/>.</param>
    void Set(string key, string? value);

    /// <summary>
    /// Reads every stored setting.
    /// </summary>
    /// <returns>The stored settings, ordered by key.</returns>
    IReadOnlyDictionary<string, string?> GetAll();
}
