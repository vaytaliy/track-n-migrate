using System.Data;
using System.IO;
using System.Reflection;
using Dapper;
using MailIntegrator.Models;
using Microsoft.Data.Sqlite;

namespace MailIntegrator.Data;

/// <summary>
/// Owns the SQLite connection string and applies versioned schema migrations on startup.
/// </summary>
/// <remarks>
/// Migrations are embedded <c>.sql</c> resources named <c>NNN_description.sql</c>; the numeric prefix is
/// the schema version and is tracked in SQLite's <c>user_version</c> pragma.
/// </remarks>
public sealed class AppDatabase
{
    private const string MigrationResourceMarker = ".Data.Sql.";

    private static readonly object HandlerRegistrationLock = new();
    private static bool _typeHandlersRegistered;

    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDatabase"/> class.
    /// </summary>
    /// <param name="databaseFilePath">Absolute path to the SQLite database file.</param>
    public AppDatabase(string databaseFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseFilePath);
        DatabaseFilePath = databaseFilePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();

        RegisterTypeHandlers();
    }

    /// <summary>
    /// Gets the absolute path of the database file backing this instance.
    /// </summary>
    public string DatabaseFilePath { get; }

    /// <summary>
    /// Creates the database file and directory when missing, then applies pending migrations.
    /// </summary>
    public void Initialize()
    {
        var directory = Path.GetDirectoryName(DatabaseFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection();
        var currentVersion = connection.ExecuteScalar<long>("PRAGMA user_version;");

        foreach (var migration in EnumerateMigrations().Where(m => m.Version > currentVersion).OrderBy(m => m.Version))
        {
            using var transaction = connection.BeginTransaction();
            connection.Execute(migration.Sql, transaction: transaction);
            connection.Execute($"PRAGMA user_version = {migration.Version};", transaction: transaction);
            transaction.Commit();
        }
    }

    /// <summary>
    /// Opens a new, ready-to-use connection to the database.
    /// </summary>
    /// <returns>An open <see cref="SqliteConnection"/>; the caller owns its lifetime.</returns>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Registers the Dapper type handlers used by every repository exactly once per process.
    /// </summary>
    /// <remarks>
    /// <see cref="SqlMapper.RemoveTypeMap(System.Type)"/> is called first because Dapper consults its
    /// built-in type map before the registered handlers when it builds command parameters. Without this,
    /// writes would silently fall back to the provider's own DateTime format while reads would use
    /// <see cref="UtcDateTimeTypeHandler"/>, leaving the two directions inconsistent. The same applies to
    /// the enum built-in mapping, which would otherwise store a status as its numeric value.
    /// </remarks>
    private static void RegisterTypeHandlers()
    {
        lock (HandlerRegistrationLock)
        {
            if (_typeHandlersRegistered)
            {
                return;
            }

            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.AddTypeHandler(new UtcDateTimeTypeHandler());

            SqlMapper.RemoveTypeMap(typeof(ParcelStatus));
            SqlMapper.AddTypeHandler(new ParcelStatusTypeHandler());
            _typeHandlersRegistered = true;
        }
    }

    /// <summary>
    /// Reads the embedded migration scripts and pairs each one with its schema version.
    /// </summary>
    /// <returns>The available migrations.</returns>
    private static IEnumerable<(long Version, string Sql)> EnumerateMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            var markerIndex = resourceName.IndexOf(MigrationResourceMarker, StringComparison.Ordinal);
            if (markerIndex < 0 || !resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = resourceName[(markerIndex + MigrationResourceMarker.Length)..];
            var versionText = new string(fileName.TakeWhile(char.IsDigit).ToArray());
            if (!long.TryParse(versionText, out var version))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            yield return (version, reader.ReadToEnd());
        }
    }
}
