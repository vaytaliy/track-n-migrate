using Dapper;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;

namespace MailIntegrator.Data;

/// <summary>
/// Dapper based SQLite implementation of <see cref="IAppSettingsRepository"/>.
/// </summary>
public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly AppDatabase _database;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsRepository"/> class.
    /// </summary>
    /// <param name="database">The database that supplies connections.</param>
    /// <param name="clock">The clock used to timestamp writes.</param>
    public AppSettingsRepository(AppDatabase database, IClock clock)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        return connection.ExecuteScalar<string?>(
            "SELECT Value FROM AppSettings WHERE Key = $key;",
            new { key });
    }

    /// <inheritdoc />
    public void Set(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var connection = _database.OpenConnection();
        connection.Execute("""
            INSERT INTO AppSettings (Key, Value, UpdatedAtUtc)
            VALUES ($key, $value, $updatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            new
            {
                key,
                value,
                updatedAtUtc = _clock.UtcNow,
            });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> GetAll()
    {
        using var connection = _database.OpenConnection();
        var rows = connection.Query<AppSetting>(
            "SELECT Key, Value, UpdatedAtUtc FROM AppSettings ORDER BY Key;");

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            result[row.Key] = row.Value;
        }

        return result;
    }
}
