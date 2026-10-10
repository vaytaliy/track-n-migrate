using System.IO;
using System.Text.Json;

namespace MailIntegrator.Configuration;

/// <summary>
/// Reads and writes <see cref="AppConfig"/> from the <c>appConfig.json</c> file.
/// </summary>
/// <remarks>
/// A missing file is seeded with <see cref="AppConfig.CreateDefault"/>, so a first run always leaves an
/// editable file behind. Malformed JSON fails fast with the file path in the message instead of silently
/// falling back to the defaults, because a wrong mapping would otherwise surface as unexplained
/// <see cref="Models.ParcelStatus.Unknown"/> statuses.
/// </remarks>
public sealed class AppConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppConfigStore"/> class.
    /// </summary>
    /// <param name="filePath">The absolute path of the configuration file.</param>
    public AppConfigStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the absolute path of the configuration file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Loads the configuration, creating the file with the defaults when it does not exist yet.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="InvalidDataException">The file exists but cannot be read as the expected JSON.</exception>
    public AppConfig Load()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = AppConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize<AppConfig>(stream, SerializerOptions)
                ?? throw new InvalidDataException($"Configuration file '{_filePath}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Configuration file '{_filePath}' is not a valid appConfig.json.",
                exception);
        }
    }

    /// <summary>
    /// Writes the configuration back to disk.
    /// </summary>
    /// <param name="config">The configuration to persist.</param>
    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(_filePath);
        JsonSerializer.Serialize(stream, config, SerializerOptions);
    }
}
