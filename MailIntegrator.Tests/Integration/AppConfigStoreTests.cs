using System.IO;
using MailIntegrator.Configuration;

namespace MailIntegrator.Tests.Integration;

/// <summary>
/// Covers loading, seeding and round-tripping of <c>appConfig.json</c>.
/// </summary>
public sealed class AppConfigStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"MailIntegrator.ConfigTests.{Guid.NewGuid():N}");

    [Fact]
    public void Load_seeds_the_file_with_the_defaults_when_it_does_not_exist()
    {
        var store = new AppConfigStore(Path.Combine(_directory, "appConfig.json"));

        var config = store.Load();

        Assert.True(File.Exists(store.FilePath));
        Assert.True(config.StatusMappings.ContainsKey("PochtaRussia"));
        Assert.Equal("Delivered", config.StatusMappings["PochtaRussia"]["Вручение"]);
    }

    [Fact]
    public void Load_round_trips_edited_values()
    {
        var store = new AppConfigStore(Path.Combine(_directory, "appConfig.json"));
        store.Save(new AppConfig
        {
            StatusMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["DHL"] = new() { ["Delivered"] = "Delivered" },
            },
        });

        var reloaded = store.Load();

        Assert.Equal("Delivered", reloaded.StatusMappings["DHL"]["Delivered"]);
    }

    [Fact]
    public void Load_reports_malformed_json_with_the_file_path()
    {
        var path = Path.Combine(_directory, "appConfig.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ not json");
        var store = new AppConfigStore(path);

        var exception = Assert.Throws<InvalidDataException>(() => store.Load());

        Assert.Contains("appConfig.json", exception.Message, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
