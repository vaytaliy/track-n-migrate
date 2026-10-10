using System.IO;
using MailIntegrator.Data;
using Microsoft.Data.Sqlite;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Creates a real SQLite database in the temp folder, applies the migrations and removes it afterwards.
/// </summary>
/// <remarks>
/// The repository layer is exercised against genuine SQLite rather than a mock so that the storage
/// format of every column is covered by the tests.
/// </remarks>
public sealed class TempDatabase : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TempDatabase"/> class with an initialised schema.
    /// </summary>
    public TempDatabase()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"mailintegrator-tests-{Guid.NewGuid():N}.db");
        Database = new AppDatabase(FilePath);
        Database.Initialize();
    }

    /// <summary>
    /// Gets the path of the temporary database file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the initialised database.
    /// </summary>
    public AppDatabase Database { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections, so the file stays locked until the pools are cleared.
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(FilePath);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test over.
        }
    }
}
