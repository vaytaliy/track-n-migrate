using Dapper;
using MailIntegrator.Data;
using MailIntegrator.Models;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests.Integration;

/// <summary>
/// Exercises the parcel repository against a real SQLite database.
/// </summary>
public sealed class ParcelRepositoryTests
{
    [Fact]
    public void Insert_assigns_the_generated_identifier()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        var parcel = SampleParcel("RU001");

        var id = repository.Insert(parcel);

        Assert.True(id > 0);
        Assert.Equal(id, parcel.Id);
    }

    /// <summary>
    /// Regression test: writes must use the explicit ISO-8601 UTC text, not the provider's default
    /// DateTime format. Dapper's built-in DateTime type map used to win over the registered handler on
    /// the write path, which stored an offset-less value that reads could not parse.
    /// </summary>
    [Fact]
    public void Insert_stores_instants_as_iso8601_utc_text()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        var parcel = SampleParcel("RU002");
        repository.Insert(parcel);

        using var connection = temp.Database.OpenConnection();
        var stored = connection.ExecuteScalar<string>(
            "SELECT CreatedDatetimeUtc FROM Parcels WHERE Id = $id;",
            new { id = parcel.Id });

        Assert.Equal("2026-10-03T09:15:10.0000000Z", stored);
    }

    [Fact]
    public void GetById_round_trips_every_column()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU003");
        repository.Insert(parcel);

        var loaded = repository.GetById(parcel.Id);

        Assert.NotNull(loaded);
        Assert.Equal(parcel.PaymentNumber, loaded!.PaymentNumber);
        Assert.Equal(parcel.TrackId, loaded.TrackId);
        Assert.Equal(parcel.TrackingServiceCode, loaded.TrackingServiceCode);
        Assert.Equal(parcel.CreatedDatetimeUtc, loaded.CreatedDatetimeUtc);
        Assert.Equal(parcel.SentDatetimeUtc, loaded.SentDatetimeUtc);
        Assert.Equal(parcel.ReceivedDatetimeUtc, loaded.ReceivedDatetimeUtc);
        Assert.Equal(parcel.LastCheckedDatetimeUtc, loaded.LastCheckedDatetimeUtc);
        Assert.Equal(parcel.LastStatus, loaded.LastStatus);
        Assert.Equal(parcel.Comment, loaded.Comment);
        Assert.Equal(parcel.IsMigratedTo1CFlag, loaded.IsMigratedTo1CFlag);
        Assert.Equal(parcel.MigratedTo1CDatetimeUtc, loaded.MigratedTo1CDatetimeUtc);
    }

    [Fact]
    public void GetById_round_trips_nulls_and_reports_utc_kind()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU004");
        parcel.SentDatetimeUtc = null;
        parcel.ReceivedDatetimeUtc = null;
        parcel.LastCheckedDatetimeUtc = null;
        parcel.LastStatus = null;
        parcel.TrackingServiceCode = null;
        parcel.Comment = null;
        parcel.MigratedTo1CDatetimeUtc = null;
        repository.Insert(parcel);

        var loaded = repository.GetById(parcel.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.SentDatetimeUtc);
        Assert.Null(loaded.ReceivedDatetimeUtc);
        Assert.Null(loaded.LastCheckedDatetimeUtc);
        Assert.Null(loaded.LastStatus);
        Assert.Null(loaded.TrackingServiceCode);
        Assert.Null(loaded.Comment);
        Assert.Null(loaded.MigratedTo1CDatetimeUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedDatetimeUtc.Kind);
    }

    [Fact]
    public void GetById_returns_null_for_an_unknown_identifier()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        Assert.Null(repository.GetById(4242));
    }

    [Fact]
    public void GetAll_orders_by_creation_instant_descending()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        var oldest = SampleParcel("RU100");
        oldest.CreatedDatetimeUtc = new DateTime(2026, 10, 1, 7, 0, 0, DateTimeKind.Utc);
        var newest = SampleParcel("RU200");
        newest.CreatedDatetimeUtc = new DateTime(2026, 10, 3, 7, 0, 0, DateTimeKind.Utc);
        var middle = SampleParcel("RU150");
        middle.CreatedDatetimeUtc = new DateTime(2026, 10, 2, 7, 0, 0, DateTimeKind.Utc);

        repository.Insert(oldest);
        repository.Insert(newest);
        repository.Insert(middle);

        var trackIds = repository.GetAll().Select(parcel => parcel.TrackId).ToList();

        Assert.Equal(["RU200", "RU150", "RU100"], trackIds);
    }

    [Fact]
    public void Insert_accepts_a_duplicate_tracking_number()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        repository.Insert(SampleParcel("RU-SHARED"));
        repository.Insert(SampleParcel("ru-shared", "PAY-SHARED-2"));

        Assert.Equal(2, repository.GetAll().Count);
    }

    [Fact]
    public void PaymentNumberExists_is_case_insensitive()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        repository.Insert(SampleParcel("RU555"));

        Assert.True(repository.PaymentNumberExists("pay-RU555"));
        Assert.True(repository.PaymentNumberExists("PAY-RU555"));
        Assert.False(repository.PaymentNumberExists("PAY-RU999"));
    }

    [Fact]
    public void PaymentNumberExists_ignores_the_excluded_row()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU556");
        repository.Insert(parcel);

        Assert.False(repository.PaymentNumberExists("PAY-RU556", parcel.Id));
        Assert.True(repository.PaymentNumberExists("PAY-RU556"));
    }

    [Fact]
    public void PaymentNumberExists_trims_the_candidate()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        repository.Insert(SampleParcel("RU557"));

        Assert.True(repository.PaymentNumberExists("  PAY-RU557  "));
    }

    [Fact]
    public void GetByTrackId_returns_every_parcel_sharing_the_provider_and_tracking_number()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var first = SampleParcel("RU-SHARED");
        var second = SampleParcel("ru-shared", "PAY-SHARED-2");
        var other = SampleParcel("RU-OTHER");
        other.TrackingServiceCode = "DHL";
        repository.Insert(first);
        repository.Insert(second);
        repository.Insert(other);

        var matching = repository.GetByTrackId("PochtaRussia", "RU-SHARED");

        Assert.Equal([first.Id, second.Id], matching.Select(parcel => parcel.Id));
    }

    [Fact]
    public void GetByTrackId_returns_an_empty_list_for_an_unknown_tracking_number()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        repository.Insert(SampleParcel("RU-KNOWN"));

        Assert.Empty(repository.GetByTrackId("PochtaRussia", "RU-UNKNOWN"));
    }

    [Fact]
    public void Update_persists_the_mutable_columns()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU600");
        repository.Insert(parcel);

        parcel.PaymentNumber = "PAY-RU601";
        parcel.TrackId = "RU601";
        parcel.Comment = "Обновлено";
        parcel.LastStatus = ParcelStatus.Delivered;
        parcel.LastCheckedDatetimeUtc = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);

        Assert.True(repository.Update(parcel));

        var loaded = repository.GetById(parcel.Id);
        Assert.Equal("PAY-RU601", loaded!.PaymentNumber);
        Assert.Equal("RU601", loaded.TrackId);
        Assert.Equal("Обновлено", loaded.Comment);
        Assert.Equal(ParcelStatus.Delivered, loaded.LastStatus);
        Assert.Equal(new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc), loaded.LastCheckedDatetimeUtc);

        // The provider is written once at insert and is deliberately excluded from the update statement.
        Assert.Equal("PochtaRussia", loaded.TrackingServiceCode);
    }

    /// <summary>
    /// Guards the enum storage format: a status must be persisted as its name, not as its numeric value,
    /// so the column stays readable and survives member reordering.
    /// </summary>
    [Fact]
    public void Insert_stores_the_status_as_its_enum_name()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU-STATUS");
        parcel.LastStatus = ParcelStatus.OutForDelivery;
        repository.Insert(parcel);

        using var connection = temp.Database.OpenConnection();
        var stored = connection.ExecuteScalar<string>(
            "SELECT LastStatus FROM Parcels WHERE Id = $id;",
            new { id = parcel.Id });

        Assert.Equal("OutForDelivery", stored);
    }

    [Fact]
    public void Update_reports_false_for_an_unknown_identifier()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        Assert.False(repository.Update(SampleParcel("RU700")));
    }

    [Fact]
    public void Delete_removes_a_parcel_that_was_not_exported()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU800");
        repository.Insert(parcel);

        Assert.True(repository.Delete(parcel.Id));
        Assert.Null(repository.GetById(parcel.Id));
    }

    /// <summary>
    /// The lock rule is also enforced in SQL so a migrated parcel cannot be removed by a caller that
    /// bypasses the service layer.
    /// </summary>
    [Fact]
    public void Delete_refuses_a_parcel_that_was_exported_to_1C()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var parcel = SampleParcel("RU900");
        parcel.IsMigratedTo1CFlag = true;
        parcel.MigratedTo1CDatetimeUtc = new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);
        repository.Insert(parcel);

        Assert.False(repository.Delete(parcel.Id));
        Assert.NotNull(repository.GetById(parcel.Id));
    }

    [Fact]
    public void Delete_reports_false_for_an_unknown_identifier()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);

        Assert.False(repository.Delete(1234));
    }

    /// <summary>Creates a fully populated parcel with a distinct payment and tracking number.</summary>
    /// <param name="trackId">The tracking number.</param>
    /// <param name="paymentNumber">The payment number, defaulted from the tracking number.</param>
    private static Parcel SampleParcel(string trackId, string? paymentNumber = null) => new()
    {
        PaymentNumber = paymentNumber ?? $"PAY-{trackId}",
        TrackId = trackId,
        TrackingServiceCode = "PochtaRussia",
        CreatedDatetimeUtc = new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc),
        SentDatetimeUtc = new DateTime(2026, 10, 3, 10, 30, 0, DateTimeKind.Utc),
        ReceivedDatetimeUtc = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc),
        LastCheckedDatetimeUtc = new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Utc),
        LastStatus = ParcelStatus.InTransit,
        Comment = "Комментарий",
        IsMigratedTo1CFlag = false,
        MigratedTo1CDatetimeUtc = null,
    };
}
