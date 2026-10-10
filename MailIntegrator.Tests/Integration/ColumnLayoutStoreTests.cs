using MailIntegrator.Data;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests.Integration;

/// <summary>
/// Covers the comma-separated persistence of the visible-column set.
/// </summary>
public sealed class ColumnLayoutStoreTests
{
    [Fact]
    public void Load_returns_null_when_nothing_was_stored()
    {
        using var temp = new TempDatabase();

        Assert.Null(CreateSut(temp).LoadVisibleColumnKeys());
    }

    [Fact]
    public void Save_then_Load_round_trips_the_keys()
    {
        using var temp = new TempDatabase();
        var store = CreateSut(temp);

        store.SaveVisibleColumnKeys(["PaymentNumber", "TrackId", "Status"]);

        Assert.Equal(["PaymentNumber", "TrackId", "Status"], store.LoadVisibleColumnKeys());
    }

    [Fact]
    public void Save_overwrites_the_previous_set()
    {
        using var temp = new TempDatabase();
        var store = CreateSut(temp);

        store.SaveVisibleColumnKeys(["PaymentNumber", "TrackId"]);
        store.SaveVisibleColumnKeys(["Comment"]);

        Assert.Equal(["Comment"], store.LoadVisibleColumnKeys());
    }

    [Fact]
    public void Load_trims_entries_and_drops_empty_and_duplicate_keys()
    {
        using var temp = new TempDatabase();
        var repository = new AppSettingsRepository(temp.Database, CreateClock());
        repository.Set(ColumnLayoutStore.SettingKey, " PaymentNumber ,, TrackId ,PaymentNumber, ");

        var keys = new ColumnLayoutStore(repository).LoadVisibleColumnKeys();

        Assert.Equal(["PaymentNumber", "TrackId"], keys);
    }

    [Fact]
    public void Save_skips_blank_keys()
    {
        using var temp = new TempDatabase();
        var store = CreateSut(temp);

        store.SaveVisibleColumnKeys(["PaymentNumber", "  ", "TrackId"]);

        Assert.Equal(["PaymentNumber", "TrackId"], store.LoadVisibleColumnKeys());
    }

    /// <summary>Builds the store under test against a real temporary database.</summary>
    private static ColumnLayoutStore CreateSut(TempDatabase temp) =>
        new(new AppSettingsRepository(temp.Database, CreateClock()));

    /// <summary>Creates a deterministic clock for the settings timestamps.</summary>
    private static FakeClock CreateClock() =>
        new(new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc));
}
