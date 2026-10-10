using System.IO;

namespace MailIntegrator.Infrastructure;

/// <summary>
/// Resolves the on-disk locations the application writes to.
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Gets the per-user directory that holds the local database file.
    /// </summary>
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MailIntegrator");

    /// <summary>
    /// Gets the full path of the SQLite database file used for parcels and non-secret settings.
    /// </summary>
    public static string DatabaseFile => Path.Combine(DataDirectory, "mailintegrator.db");
}
