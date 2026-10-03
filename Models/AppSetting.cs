namespace MailIntegrator.Models;

/// <summary>
/// A non-secret key/value pair persisted in the local database.
/// </summary>
/// <param name="Key">The setting identifier.</param>
/// <param name="Value">The setting value, or <see langword="null"/> when unset.</param>
/// <param name="UpdatedAtUtc">The instant the setting was last written, in UTC.</param>
public sealed record AppSetting(string Key, string? Value, DateTime UpdatedAtUtc);
