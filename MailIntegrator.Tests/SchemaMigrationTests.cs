using System.IO;
using Dapper;
using MailIntegrator.Data;
using MailIntegrator.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the versioned schema migrations, in particular the legacy status conversion of migration
/// <c>002</c> and the payment-number rebuild performed by migration <c>003</c>.
/// </summary>
public sealed class SchemaMigrationTests
{
    [Fact]
    public void Initialize_applies_every_migration_on_a_fresh_database()
    {
        using var temp = new TempDatabase();

        using var connection = temp.Database.OpenConnection();

        Assert.Equal(3, connection.ExecuteScalar<long>("PRAGMA user_version;"));
    }

    [Fact]
    public void Migration_002_adds_the_provider_column_and_normalises_legacy_statuses()
    {
        var path = CreateTemporaryDatabasePath();

        try
        {
            CreateVersionOneDatabase(path);

            // Apply only 002 so its conversion is still observable; 003 would discard these legacy rows.
            using var connection = OpenRawConnection(path);
            connection.Execute(ReadMigrationScript("002_tracking_service_and_status.sql"));
            connection.Execute("PRAGMA user_version = 2;");

            var statuses = connection
                .Query<string?>("SELECT LastStatus FROM Parcels ORDER BY Id;")
                .ToList();
            Assert.Equal(["Delivered", "InTransit", "Processing", "Unknown", null], statuses);

            var providers = connection
                .Query<string?>("SELECT TrackingServiceCode FROM Parcels ORDER BY Id;")
                .ToList();
            Assert.All(providers, Assert.Null);
        }
        finally
        {
            CleanupTemporaryDatabase(path);
        }
    }

    /// <summary>
    /// Builds a real version 2 database with legacy rows, then lets the migration runner upgrade it, so the
    /// rebuild is exercised against genuine SQLite rather than asserted from the script text.
    /// </summary>
    [Fact]
    public void Migration_003_rebuilds_the_table_for_the_payment_number_key()
    {
        var path = CreateTemporaryDatabasePath();

        try
        {
            CreateVersionTwoDatabase(path);

            var database = new AppDatabase(path);
            database.Initialize();

            using var connection = database.OpenConnection();
            Assert.Equal(3, connection.ExecuteScalar<long>("PRAGMA user_version;"));

            // The legacy rows predate the payment number and are deliberately discarded.
            Assert.Equal(0, connection.ExecuteScalar<long>("SELECT COUNT(*) FROM Parcels;"));

            var paymentNumberColumn = connection
                .Query<(long Cid, string Name, string Type, long NotNull)>("PRAGMA table_info(Parcels);")
                .Single(column => column.Name == "PaymentNumber");
            Assert.Equal("TEXT", paymentNumberColumn.Type);
            Assert.Equal(1, paymentNumberColumn.NotNull);

            // A duplicated tracking number is now accepted; a duplicated payment number is not.
            // The dated insert column is stored as ISO-8601 UTC text, matching DateTimeStorage.
            const string InsertSql = """
                INSERT INTO Parcels (PaymentNumber, TrackId, CreatedDatetimeUtc)
                VALUES ($paymentNumber, $trackId, '2026-10-03T09:15:10.0000000Z');
                """;
            connection.Execute(InsertSql, new { paymentNumber = "PAY-1", trackId = "SHARED" });
            connection.Execute(InsertSql, new { paymentNumber = "PAY-2", trackId = "SHARED" });

            var duplicatePaymentNumber = Assert.Throws<SqliteException>(
                () => connection.Execute(InsertSql, new { paymentNumber = "pay-1", trackId = "OTHER" }));
            Assert.Contains("UNIQUE", duplicatePaymentNumber.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTemporaryDatabase(path);
        }
    }

    /// <summary>Creates the original version 1 schema with representative legacy status values.</summary>
    private static void CreateVersionOneDatabase(string path)
    {
        using var connection = OpenRawConnection(path);

        connection.Execute("""
            CREATE TABLE Parcels (
                Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                TrackId                 TEXT    NOT NULL,
                CreatedDatetimeUtc      TEXT    NOT NULL,
                SentDatetimeUtc         TEXT    NULL,
                ReceivedDatetimeUtc     TEXT    NULL,
                LastCheckedDatetimeUtc  TEXT    NULL,
                LastStatus              TEXT    NULL,
                Comment                 TEXT    NULL,
                IsMigratedTo1CFlag      INTEGER NOT NULL DEFAULT 0,
                MigratedTo1CDatetimeUtc TEXT    NULL
            );
            """);

        connection.Execute("""
            INSERT INTO Parcels (TrackId, CreatedDatetimeUtc, LastStatus) VALUES
                ('LEGACY-1', '2026-10-03T09:15:10.0000000Z', 'Доставлено'),
                ('LEGACY-2', '2026-10-03T09:15:10.0000000Z', 'В пути'),
                ('LEGACY-3', '2026-10-03T09:15:10.0000000Z', 'Ожидает отправки'),
                ('LEGACY-4', '2026-10-03T09:15:10.0000000Z', 'Что-то неизвестное'),
                ('LEGACY-5', '2026-10-03T09:15:10.0000000Z', NULL);
            """);

        connection.Execute("PRAGMA user_version = 1;");
    }

    /// <summary>Creates the version 2 schema, including the provider column, with legacy rows.</summary>
    private static void CreateVersionTwoDatabase(string path)
    {
        CreateVersionOneDatabase(path);

        using var connection = OpenRawConnection(path);
        connection.Execute("ALTER TABLE Parcels ADD COLUMN TrackingServiceCode TEXT NULL;");
        connection.Execute("PRAGMA user_version = 2;");
    }

    /// <summary>Reads an embedded migration script by file name.</summary>
    /// <param name="fileName">The script file name, for example <c>002_tracking_service_and_status.sql</c>.</param>
    /// <returns>The script text.</returns>
    private static string ReadMigrationScript(string fileName)
    {
        var assembly = typeof(AppDatabase).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith($".Data.Sql.{fileName}", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Opens a raw connection to a database file outside the migration runner.</summary>
    /// <param name="path">The database file path.</param>
    /// <returns>The open connection, owned by the caller.</returns>
    private static SqliteConnection OpenRawConnection(string path)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        return connection;
    }

    /// <summary>Builds a unique temporary database path.</summary>
    /// <returns>The path, which does not exist yet.</returns>
    private static string CreateTemporaryDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"mailintegrator-migration-{Guid.NewGuid():N}.db");

    /// <summary>Clears pooled connections and deletes a temporary database file.</summary>
    /// <param name="path">The database file path.</param>
    private static void CleanupTemporaryDatabase(string path)
    {
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }
    }
}
