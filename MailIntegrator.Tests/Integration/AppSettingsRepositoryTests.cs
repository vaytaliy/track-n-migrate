using MailIntegrator.Data;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests.Integration;

/// <summary>
/// Covers the non-secret settings store.
/// </summary>
public sealed class AppSettingsRepositoryTests
{
    [Fact]
    public void Get_returns_null_for_an_absent_key()
    {
        using var temp = new TempDatabase();
        var repository = CreateSut(temp);

        Assert.Null(repository.Get("missing"));
    }

    [Fact]
    public void Set_then_Get_round_trips_the_value()
    {
        using var temp = new TempDatabase();
        var repository = CreateSut(temp);

        repository.Set("Ui.LastTab", "parcels");

        Assert.Equal("parcels", repository.Get("Ui.LastTab"));
    }

    [Fact]
    public void Set_stamps_the_update_instant_from_the_clock()
    {
        using var temp = new TempDatabase();
        var clock = new FakeClock(new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc));
        var repository = new AppSettingsRepository(temp.Database, clock);

        repository.Set("Key", "Value");

        var settings = repository.GetAll();
        Assert.True(settings.ContainsKey("Key"));
    }

    [Fact]
    public void Set_overwrites_an_existing_value()
    {
        using var temp = new TempDatabase();
        var repository = CreateSut(temp);

        repository.Set("Key", "first");
        repository.Set("Key", "second");

        Assert.Equal("second", repository.Get("Key"));
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void Set_accepts_a_null_value()
    {
        using var temp = new TempDatabase();
        var repository = CreateSut(temp);

        repository.Set("Key", null);

        Assert.Null(repository.Get("Key"));
        Assert.True(repository.GetAll().ContainsKey("Key"));
    }

    [Fact]
    public void GetAll_returns_every_setting_ordered_by_key()
    {
        using var temp = new TempDatabase();
        var repository = CreateSut(temp);

        repository.Set("Zeta", "1");
        repository.Set("Alpha", "2");

        Assert.Equal(["Alpha", "Zeta"], repository.GetAll().Keys);
    }

    /// <summary>Builds the repository under test.</summary>
    private static AppSettingsRepository CreateSut(TempDatabase temp) =>
        new(temp.Database, new FakeClock(new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc)));
}
