using MailIntegrator.Data;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the on-disk representation of instants.
/// </summary>
public sealed class DateTimeStorageTests
{
    [Fact]
    public void ToStorage_writes_an_explicit_iso8601_utc_value()
    {
        var value = new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);

        Assert.Equal("2026-10-03T09:15:10.0000000Z", DateTimeStorage.ToStorage(value));
    }

    [Fact]
    public void ToStorage_returns_null_for_a_null_value() => Assert.Null(DateTimeStorage.ToStorage(null));

    [Fact]
    public void ToStorage_treats_an_unspecified_kind_as_utc()
    {
        // View models and tests frequently hand over values without a Kind; they must not be shifted
        // by the machine's local offset.
        var value = new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Unspecified);

        Assert.Equal("2026-10-03T09:15:10.0000000Z", DateTimeStorage.ToStorage(value));
    }

    [Fact]
    public void FromStorage_round_trips_and_reports_a_utc_kind()
    {
        var original = new DateTime(2026, 10, 3, 9, 15, 10, 123, DateTimeKind.Utc);

        var parsed = DateTimeStorage.FromStorage(DateTimeStorage.ToStorage(original));

        Assert.Equal(original, parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
    }

    [Fact]
    public void FromStorage_returns_null_for_null_database_null_and_empty_text()
    {
        Assert.Null(DateTimeStorage.FromStorage(null));
        Assert.Null(DateTimeStorage.FromStorage(DBNull.Value));
        Assert.Null(DateTimeStorage.FromStorage(string.Empty));
        Assert.Null(DateTimeStorage.FromStorage("   "));
    }

    /// <summary>
    /// Regression test: a value written by the SQLite provider's own DateTime handling must not be
    /// accepted silently, because that format carries no offset information.
    /// </summary>
    [Fact]
    public void FromStorage_rejects_the_providers_default_format()
    {
        Assert.Throws<FormatException>(() => DateTimeStorage.FromStorage("2026-10-03 05:00:00"));
    }
}
