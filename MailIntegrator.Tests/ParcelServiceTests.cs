using MailIntegrator.Data;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the parcel business rules.
/// </summary>
public sealed class ParcelServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);
    private const string ProviderCode = "PochtaRussia";

    [Fact]
    public void CreateParcel_applies_the_documented_defaults()
    {
        using var temp = new TempDatabase();
        var (service, repository, clock) = CreateSut(temp);

        var parcel = service.CreateParcel("PAY-123", "RU123456789CN", ProviderCode);

        Assert.Equal("PAY-123", parcel.PaymentNumber);
        Assert.Equal("RU123456789CN", parcel.TrackId);
        Assert.Equal(ProviderCode, parcel.TrackingServiceCode);
        Assert.Equal(clock.UtcNow, parcel.CreatedDatetimeUtc);
        Assert.False(parcel.IsMigratedTo1CFlag);
        Assert.Null(parcel.SentDatetimeUtc);
        Assert.Null(parcel.ReceivedDatetimeUtc);
        Assert.Null(parcel.LastCheckedDatetimeUtc);
        Assert.Null(parcel.LastStatus);
        Assert.Null(parcel.Comment);
        Assert.Null(parcel.MigratedTo1CDatetimeUtc);
        Assert.NotNull(repository.GetById(parcel.Id));
    }

    [Fact]
    public void CreateParcel_stamps_the_creation_instant_from_the_clock()
    {
        using var temp = new TempDatabase();
        var (service, _, clock) = CreateSut(temp);

        clock.Advance(TimeSpan.FromMinutes(5));
        var parcel = service.CreateParcel("PAY-ADVANCE", "RU-ADVANCE", ProviderCode);

        Assert.Equal(FixedNow.AddMinutes(5), parcel.CreatedDatetimeUtc);
    }

    [Theory]
    [InlineData("  PAY-1  ", "  RU123456789CN  ", "PAY-1", "RU123456789CN")]
    [InlineData("\tPAY-2\n", "\tRU2\n", "PAY-2", "RU2")]
    public void CreateParcel_trims_both_identifiers(
        string paymentNumber,
        string trackId,
        string expectedPaymentNumber,
        string expectedTrackId)
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        var parcel = service.CreateParcel(paymentNumber, trackId, ProviderCode);

        Assert.Equal(expectedPaymentNumber, parcel.PaymentNumber);
        Assert.Equal(expectedTrackId, parcel.TrackId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateParcel_rejects_a_missing_payment_number(string paymentNumber)
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);

        Assert.Throws<ParcelValidationException>(
            () => service.CreateParcel(paymentNumber, "RU-NO-PAYMENT", ProviderCode));
        Assert.Empty(repository.GetAll());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateParcel_rejects_a_missing_tracking_number(string trackId)
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);

        Assert.Throws<ParcelValidationException>(
            () => service.CreateParcel("PAY-NO-TRACK", trackId, ProviderCode));
        Assert.Empty(repository.GetAll());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownCarrier")]
    public void CreateParcel_rejects_a_missing_or_unregistered_tracking_provider(string providerCode)
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);

        Assert.Throws<ParcelValidationException>(
            () => service.CreateParcel("PAY-PROVIDER", "RU-PROVIDER", providerCode));
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void CreateParcel_rejects_a_duplicate_payment_number_regardless_of_case()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        service.CreateParcel("PAY123456789", "RU-FIRST", ProviderCode);

        Assert.Throws<ParcelValidationException>(
            () => service.CreateParcel("pay123456789", "RU-SECOND", ProviderCode));
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void CreateParcel_accepts_a_duplicate_tracking_number()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);

        service.CreateParcel("PAY-ONE", "RU-SHARED", ProviderCode);
        service.CreateParcel("PAY-TWO", "ru-shared", ProviderCode);

        Assert.Equal(2, repository.GetAll().Count);
    }

    [Fact]
    public void IsPaymentNumberAvailable_reports_false_for_a_stored_payment_number()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-EXISTS", "RU-EXISTS", ProviderCode);

        Assert.False(service.IsPaymentNumberAvailable("PAY-EXISTS"));
        Assert.False(service.IsPaymentNumberAvailable("pay-exists"));
        Assert.True(service.IsPaymentNumberAvailable("PAY-EXISTS", parcel.Id));
        Assert.True(service.IsPaymentNumberAvailable("PAY-OTHER"));
    }

    [Fact]
    public void IsPaymentNumberAvailable_reports_false_for_a_blank_candidate()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        Assert.False(service.IsPaymentNumberAvailable("   "));
    }

    [Fact]
    public void ApplyTrackingResult_updates_the_status_and_the_status_instant()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-STATUS", "RU-STATUS", ProviderCode);
        var statusInstant = FixedNow.AddHours(6);

        var applied = service.ApplyTrackingResult(
            parcel.Id,
            new TrackingResult("RU-STATUS", ParcelStatus.Customs, statusInstant));

        Assert.True(applied);
        var stored = repository.GetById(parcel.Id)!;
        Assert.Equal(ParcelStatus.Customs, stored.LastStatus);
        Assert.Equal(statusInstant, stored.LastCheckedDatetimeUtc);
        Assert.Equal(ProviderCode, stored.TrackingServiceCode);
    }

    [Theory]
    [InlineData(ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Exception)]
    public void ApplyTrackingResult_ignores_a_parcel_that_reached_a_final_status(ParcelStatus finalStatus)
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-FINAL", "RU-FINAL", ProviderCode);
        parcel.LastStatus = finalStatus;
        parcel.LastCheckedDatetimeUtc = FixedNow.AddHours(1);
        repository.Update(parcel);

        var applied = service.ApplyTrackingResult(
            parcel.Id,
            new TrackingResult("RU-FINAL", ParcelStatus.InTransit, FixedNow.AddHours(5)));

        Assert.False(applied);
        Assert.Equal(finalStatus, repository.GetById(parcel.Id)!.LastStatus);
    }

    [Fact]
    public void ApplyTrackingResult_reports_false_for_an_unknown_parcel()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        var applied = service.ApplyTrackingResult(
            999,
            new TrackingResult("RU-NONE", ParcelStatus.InTransit, FixedNow));

        Assert.False(applied);
    }

    [Fact]
    public void ApplyTrackingResultToTrackId_updates_every_parcel_sharing_the_tracking_number()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var first = service.CreateParcel("PAY-FAN-1", "RU-SHARED", ProviderCode);
        var second = service.CreateParcel("PAY-FAN-2", "ru-shared", ProviderCode);
        var other = service.CreateParcel("PAY-FAN-3", "RU-OTHER", ProviderCode);
        var statusInstant = FixedNow.AddHours(4);

        var updated = service.ApplyTrackingResultToTrackId(
            ProviderCode,
            "RU-SHARED",
            new TrackingResult("RU-SHARED", ParcelStatus.Delivered, statusInstant));

        Assert.Equal(2, updated);
        Assert.Equal(ParcelStatus.Delivered, repository.GetById(first.Id)!.LastStatus);
        Assert.Equal(statusInstant, repository.GetById(second.Id)!.LastCheckedDatetimeUtc);
        Assert.Null(repository.GetById(other.Id)!.LastStatus);
    }

    [Fact]
    public void ApplyTrackingResultToTrackId_skips_parcels_that_reached_a_final_status()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var delivered = service.CreateParcel("PAY-DONE", "RU-SHARED", ProviderCode);
        delivered.LastStatus = ParcelStatus.Delivered;
        delivered.LastCheckedDatetimeUtc = FixedNow.AddHours(1);
        repository.Update(delivered);
        var pending = service.CreateParcel("PAY-PENDING", "RU-SHARED", ProviderCode);

        var updated = service.ApplyTrackingResultToTrackId(
            ProviderCode,
            "RU-SHARED",
            new TrackingResult("RU-SHARED", ParcelStatus.InTransit, FixedNow.AddHours(4)));

        Assert.Equal(1, updated);
        Assert.Equal(ParcelStatus.Delivered, repository.GetById(delivered.Id)!.LastStatus);
        Assert.Equal(ParcelStatus.InTransit, repository.GetById(pending.Id)!.LastStatus);
    }

    [Fact]
    public void ApplyTrackingResultToTrackId_reports_zero_for_an_unknown_tracking_number()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        var updated = service.ApplyTrackingResultToTrackId(
            ProviderCode,
            "RU-UNKNOWN",
            new TrackingResult("RU-UNKNOWN", ParcelStatus.InTransit, FixedNow));

        Assert.Equal(0, updated);
    }

    [Fact]
    public void UpdateEditableFields_changes_the_payment_number_tracking_number_and_comment()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-EDIT", "RU-EDIT", ProviderCode);

        var updated = service.UpdateEditableFields(
            parcel.Id,
            "  PAY-EDITED  ",
            "  RU-EDITED  ",
            "  Принято  ");

        Assert.Equal("PAY-EDITED", updated.PaymentNumber);
        Assert.Equal("RU-EDITED", updated.TrackId);
        Assert.Equal("Принято", updated.Comment);
        Assert.Equal(ProviderCode, updated.TrackingServiceCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateEditableFields_stores_an_empty_comment_as_null(string? comment)
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-COMMENT", "RU-COMMENT", ProviderCode);

        var updated = service.UpdateEditableFields(parcel.Id, "PAY-COMMENT", "RU-COMMENT", comment);

        Assert.Null(updated.Comment);
    }

    [Fact]
    public void UpdateEditableFields_allows_keeping_the_same_payment_number()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-SAME", "RU-SAME", ProviderCode);

        var updated = service.UpdateEditableFields(parcel.Id, "PAY-SAME", "RU-SAME", "ok");

        Assert.Equal("PAY-SAME", updated.PaymentNumber);
        Assert.Equal("ok", updated.Comment);
    }

    [Fact]
    public void UpdateEditableFields_rejects_a_payment_number_used_by_another_parcel()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        service.CreateParcel("PAY-FIRST", "RU-FIRST", ProviderCode);
        var second = service.CreateParcel("PAY-SECOND", "RU-SECOND", ProviderCode);

        Assert.Throws<ParcelValidationException>(
            () => service.UpdateEditableFields(second.Id, "pay-first", "RU-SECOND", null));
    }

    [Fact]
    public void UpdateEditableFields_rejects_a_missing_tracking_number()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-NO-TRACK", "RU-NO-TRACK", ProviderCode);

        Assert.Throws<ParcelValidationException>(
            () => service.UpdateEditableFields(parcel.Id, "PAY-NO-TRACK", "   ", null));
    }

    [Fact]
    public void UpdateEditableFields_rejects_a_parcel_exported_to_1C()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var migrated = InsertMigrated(repository, "PAY-LOCKED", "RU-LOCKED");

        Assert.Throws<ParcelValidationException>(
            () => service.UpdateEditableFields(migrated.Id, "PAY-CHANGED", "RU-CHANGED", "nope"));
    }

    [Fact]
    public void UpdateEditableFields_rejects_an_unknown_parcel()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        Assert.Throws<ParcelValidationException>(
            () => service.UpdateEditableFields(999, "PAY-NONE", "RU-NONE", null));
    }

    [Fact]
    public void DeleteParcel_removes_a_parcel_that_was_not_exported()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var parcel = service.CreateParcel("PAY-DELETE", "RU-DELETE", ProviderCode);

        Assert.True(service.DeleteParcel(parcel.Id));
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void DeleteParcel_reports_false_for_an_unknown_parcel()
    {
        using var temp = new TempDatabase();
        var (service, _, _) = CreateSut(temp);

        Assert.False(service.DeleteParcel(777));
    }

    [Fact]
    public void DeleteParcel_rejects_a_parcel_exported_to_1C()
    {
        using var temp = new TempDatabase();
        var (service, repository, _) = CreateSut(temp);
        var migrated = InsertMigrated(repository, "PAY-KEEP", "RU-KEEP");

        Assert.Throws<ParcelValidationException>(() => service.DeleteParcel(migrated.Id));
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void GetAllParcels_returns_the_default_ordering()
    {
        using var temp = new TempDatabase();
        var (service, _, clock) = CreateSut(temp);

        service.CreateParcel("PAY-OLD", "RU-OLD", ProviderCode);
        clock.Advance(TimeSpan.FromHours(1));
        service.CreateParcel("PAY-NEW", "RU-NEW", ProviderCode);

        Assert.Equal(["RU-NEW", "RU-OLD"], service.GetAllParcels().Select(parcel => parcel.TrackId));
    }

    /// <summary>Builds the service under test together with its collaborators.</summary>
    private static (ParcelService Service, ParcelRepository Repository, FakeClock Clock) CreateSut(
        TempDatabase temp)
    {
        var repository = new ParcelRepository(temp.Database);
        var clock = new FakeClock(FixedNow);
        var registry = new TrackingServiceRegistry(
            [new FakeTrackingService(ProviderCode, "Почта России")]);

        return (new ParcelService(repository, clock, registry), repository, clock);
    }

    /// <summary>Stores a parcel that already carries the 1C export flag.</summary>
    private static Parcel InsertMigrated(
        ParcelRepository repository,
        string paymentNumber,
        string trackId)
    {
        var parcel = new Parcel
        {
            PaymentNumber = paymentNumber,
            TrackId = trackId,
            TrackingServiceCode = ProviderCode,
            CreatedDatetimeUtc = FixedNow,
            IsMigratedTo1CFlag = true,
            MigratedTo1CDatetimeUtc = FixedNow.AddDays(1),
        };

        repository.Insert(parcel);
        return parcel;
    }
}
