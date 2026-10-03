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

    /// <summary>
    /// Gets the full path of the editable configuration file that carries, among other settings, the
    /// provider status mappings. It is deployed next to the executable so an operator can edit it in place.
    /// </summary>
    public static string AppConfigFile => Path.Combine(AppContext.BaseDirectory, "appConfig.json");
}
