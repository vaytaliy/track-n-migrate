using MailIntegrator.Data;

namespace MailIntegrator.Services;

/// <summary>
/// Stores the visible-column set as a single comma-separated <c>AppSettings</c> value.
/// </summary>
/// <remarks>
/// The whole set lives in one row, so showing or hiding a column is a single upsert. This keeps the
/// setting trivially resettable and avoids a row per column.
/// </remarks>
public sealed class ColumnLayoutStore : IColumnLayoutStore
{
    /// <summary>
    /// The <c>AppSettings</c> key that holds the comma-separated visible-column keys.
    /// </summary>
    public const string SettingKey = "Grid.VisibleColumns";

    private readonly IAppSettingsRepository _settingsRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnLayoutStore"/> class.
    /// </summary>
    /// <param name="settingsRepository">The non-secret settings store that owns the row.</param>
    public ColumnLayoutStore(IAppSettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
    }

    /// <inheritdoc />
    public IReadOnlyList<string>? LoadVisibleColumnKeys()
    {
        var storedValue = _settingsRepository.Get(SettingKey);
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        return storedValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public void SaveVisibleColumnKeys(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var storedValue = string.Join(',', keys.Where(key => !string.IsNullOrWhiteSpace(key)));
        _settingsRepository.Set(SettingKey, storedValue);
    }
}
