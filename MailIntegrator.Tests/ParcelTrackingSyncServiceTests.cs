using System.Net;
using System.Net.Http;
using MailIntegrator.Data;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the tracking orchestration: routing, authentication, status application and skipping rules.
/// </summary>
public sealed class ParcelTrackingSyncServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_polls_every_active_parcel_and_applies_the_reported_status()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var pochtaParcel = context.InsertParcel("RU-P", "PochtaRussia");
        var dhlParcel = context.InsertParcel("RU-D", "DHL");

        await sut.RunAsync(CancellationToken.None);

        var storedPochta = context.Repository.GetById(pochtaParcel.Id)!;
        var storedDhl = context.Repository.GetById(dhlParcel.Id)!;
        Assert.Equal(ParcelStatus.Delivered, storedPochta.LastStatus);
        Assert.Equal(FixedNow.AddHours(1), storedPochta.LastCheckedDatetimeUtc);
        Assert.Equal(ParcelStatus.InTransit, storedDhl.LastStatus);
        Assert.Equal(["RU-P"], context.Pochta.RequestedTrackIds);
        Assert.Equal(["RU-D"], context.Dhl.RequestedTrackIds);
    }

    [Fact]
    public async Task RunAsync_authenticates_once_per_provider()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        context.InsertParcel("RU-P1", "PochtaRussia");
        context.InsertParcel("RU-P2", "PochtaRussia");
        context.InsertParcel("RU-D1", "DHL");

        await sut.RunAsync(CancellationToken.None);

        Assert.Equal(1, context.Pochta.AuthenticateCount);
        Assert.Equal(1, context.Dhl.AuthenticateCount);
        Assert.Equal("user-pochta", context.Pochta.LastUser);
        Assert.Equal("user-dhl", context.Dhl.LastUser);
    }

    [Theory]
    [InlineData(ParcelStatus.Delivered)]
    [InlineData(ParcelStatus.Exception)]
    public async Task RunAsync_skips_a_parcel_that_reached_a_final_status(ParcelStatus finalStatus)
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var parcel = context.InsertParcel("RU-FINAL", "PochtaRussia");
        parcel.LastStatus = finalStatus;
        context.Repository.Update(parcel);

        await sut.RunAsync(CancellationToken.None);

        Assert.Empty(context.Pochta.RequestedTrackIds);
    }

    [Fact]
    public async Task RunAsync_skips_a_parcel_without_a_provider()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        context.InsertParcel("RU-NO-PROVIDER", trackingServiceCode: null);

        await sut.RunAsync(CancellationToken.None);

        Assert.Empty(context.Pochta.RequestedTrackIds);
        Assert.Empty(context.Dhl.RequestedTrackIds);
    }

    [Fact]
    public async Task RunAsync_skips_a_parcel_whose_provider_is_no_longer_registered()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        context.InsertParcel("RU-GONE", "RemovedCarrier");

        // Must not throw for an unroutable parcel.
        await sut.RunAsync(CancellationToken.None);

        Assert.Empty(context.Pochta.RequestedTrackIds);
    }

    [Fact]
    public async Task RunAsync_reports_a_failed_tracking_number_and_keeps_polling_the_others()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        context.Pochta.Failures["RU-BAD"] = new HttpRequestException(
            "mock transport failure",
            inner: null,
            HttpStatusCode.ServiceUnavailable);
        var bad = context.InsertParcel("RU-BAD", "PochtaRussia");
        var good = context.InsertParcel("RU-GOOD", "PochtaRussia");
        var dhl = context.InsertParcel("RU-DHL", "DHL");

        // The failure must not end the pass: one bad number cannot hide the status of the others.
        await sut.RunAsync(CancellationToken.None);

        Assert.Equal(2, context.Pochta.RequestedTrackIds.Count);
        Assert.Contains("RU-BAD", context.Pochta.RequestedTrackIds);
        Assert.Contains("RU-GOOD", context.Pochta.RequestedTrackIds);
        Assert.Null(context.Repository.GetById(bad.Id)!.LastStatus);
        Assert.Equal(ParcelStatus.Delivered, context.Repository.GetById(good.Id)!.LastStatus);
        Assert.Equal(ParcelStatus.InTransit, context.Repository.GetById(dhl.Id)!.LastStatus);

        var error = Assert.Single(context.ErrorLog.Errors);
        Assert.Equal("Почта России", error.ProviderName);
        Assert.Equal("RU-BAD", error.TrackId);
        Assert.Contains("503", error.Reason, StringComparison.Ordinal);
        Assert.Contains("RU-BAD", error.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_reports_a_missing_credential_and_keeps_polling_the_other_providers()
    {
        using var context = CreateContext(configurePochtaCredentials: false);
        var sut = context.CreateService();
        context.InsertParcel("RU-P", "PochtaRussia");
        context.InsertParcel("RU-D", "DHL");

        await sut.RunAsync(CancellationToken.None);

        // The unusable provider is skipped, but its failure is reported and DHL still ran.
        Assert.Empty(context.Pochta.RequestedTrackIds);
        Assert.Equal(["RU-D"], context.Dhl.RequestedTrackIds);

        var error = Assert.Single(context.ErrorLog.Errors);
        Assert.Equal("Почта России", error.ProviderName);
        Assert.Null(error.TrackId);
        Assert.Equal("не заданы учётные данные", error.Reason);
        Assert.DoesNotContain("user-", error.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_leaves_the_error_log_empty_when_every_call_succeeds()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        context.InsertParcel("RU-P", "PochtaRussia");

        await sut.RunAsync(CancellationToken.None);

        Assert.False(context.ErrorLog.HasErrors);
        Assert.Empty(context.ErrorLog.Errors);
    }

    [Fact]
    public async Task RunForParcelAsync_polls_only_the_requested_parcel_and_applies_it()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var requested = context.InsertParcel("RU-ONE", "PochtaRussia");
        var other = context.InsertParcel("RU-TWO", "PochtaRussia");

        var result = await sut.RunForParcelAsync(requested.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ParcelStatus.Delivered, result!.CurrentStatus);
        Assert.Equal(["RU-ONE"], context.Pochta.RequestedTrackIds);
        Assert.Equal(1, context.Pochta.AuthenticateCount);
        Assert.Equal(ParcelStatus.Delivered, context.Repository.GetById(requested.Id)!.LastStatus);
        Assert.Null(context.Repository.GetById(other.Id)!.LastStatus);
    }

    [Fact]
    public async Task RunForParcelAsync_returns_null_and_does_not_poll_a_final_parcel()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var parcel = context.InsertParcel("RU-FINAL", "PochtaRussia");
        parcel.LastStatus = ParcelStatus.Delivered;
        parcel.LastCheckedDatetimeUtc = FixedNow;
        context.Repository.Update(parcel);

        var result = await sut.RunForParcelAsync(parcel.Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(context.Pochta.RequestedTrackIds);
        Assert.Equal(0, context.Pochta.AuthenticateCount);
    }

    [Fact]
    public async Task RunForParcelAsync_reports_a_missing_credential_and_returns_null()
    {
        using var context = CreateContext(configurePochtaCredentials: false);
        var sut = context.CreateService();
        var parcel = context.InsertParcel("RU-NO-CREDENTIAL", "PochtaRussia");

        var result = await sut.RunForParcelAsync(parcel.Id, CancellationToken.None);

        Assert.Null(result);

        var error = Assert.Single(context.ErrorLog.Errors);
        Assert.Equal("Почта России", error.ProviderName);
        Assert.Null(error.TrackId);
        Assert.Equal("не заданы учётные данные", error.Reason);
    }

    [Fact]
    public async Task RunAsync_polls_a_duplicate_tracking_number_once_and_updates_every_row()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var first = context.InsertParcel("RU-SHARED", "PochtaRussia", "PAY-1");
        var second = context.InsertParcel("ru-shared", "PochtaRussia", "PAY-2");

        await sut.RunAsync(CancellationToken.None);

        // One carrier call for the shared tracking number, and both rows receive the same answer.
        var requestedTrackId = Assert.Single(context.Pochta.RequestedTrackIds);
        Assert.Equal("RU-SHARED", requestedTrackId, ignoreCase: true);
        Assert.Equal(ParcelStatus.Delivered, context.Repository.GetById(first.Id)!.LastStatus);
        Assert.Equal(ParcelStatus.Delivered, context.Repository.GetById(second.Id)!.LastStatus);
    }

    [Fact]
    public async Task RunForParcelAsync_updates_every_row_sharing_the_tracking_number()
    {
        using var context = CreateContext();
        var sut = context.CreateService();
        var requested = context.InsertParcel("RU-SHARED", "PochtaRussia", "PAY-1");
        var sibling = context.InsertParcel("RU-SHARED", "PochtaRussia", "PAY-2");
        var other = context.InsertParcel("RU-OTHER", "PochtaRussia", "PAY-3");

        var result = await sut.RunForParcelAsync(requested.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(["RU-SHARED"], context.Pochta.RequestedTrackIds);
        Assert.Equal(ParcelStatus.Delivered, context.Repository.GetById(sibling.Id)!.LastStatus);
        Assert.Null(context.Repository.GetById(other.Id)!.LastStatus);
    }

    /// <summary>Builds the collaborators and a real parcel service over a temporary database.</summary>
    /// <param name="configurePochtaCredentials">Whether a credential is stored for Почта России.</param>
    /// <param name="configureDhlCredentials">Whether a credential is stored for DHL.</param>
    private static SyncContext CreateContext(
        bool configurePochtaCredentials = true,
        bool configureDhlCredentials = true)
    {
        var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var clock = new FakeClock(FixedNow);

        var pochta = new FakeTrackingService(
            "PochtaRussia",
            "Почта России",
            trackId => new TrackingResult(trackId, ParcelStatus.Delivered, FixedNow.AddHours(1)));
        var dhl = new FakeTrackingService(
            "DHL",
            "DHL",
            trackId => new TrackingResult(trackId, ParcelStatus.InTransit, FixedNow.AddHours(2)));

        var registry = new TrackingServiceRegistry([pochta, dhl]);
        var parcelService = new ParcelService(repository, clock, registry);

        var secretStore = new FakeSecretStore();
        if (configurePochtaCredentials)
        {
            secretStore.SaveCredential(pochta.Descriptor.ToCredentialTarget(), "user-pochta", "pw");
        }

        if (configureDhlCredentials)
        {
            secretStore.SaveCredential(dhl.Descriptor.ToCredentialTarget(), "user-dhl", "pw");
        }

        return new SyncContext(temp, repository, parcelService, registry, secretStore, new UserErrorLog(), pochta, dhl);
    }

    /// <summary>
    /// Holds the temporary database and collaborators so a test can seed data and inspect the fakes.
    /// </summary>
    private sealed record SyncContext(
        TempDatabase Temp,
        ParcelRepository Repository,
        ParcelService ParcelService,
        TrackingServiceRegistry Registry,
        FakeSecretStore SecretStore,
        UserErrorLog ErrorLog,
        FakeTrackingService Pochta,
        FakeTrackingService Dhl) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => Temp.Dispose();

        /// <summary>Creates the orchestration service under test.</summary>
        /// <returns>The service under test.</returns>
        public ParcelTrackingSyncService CreateService() =>
            new(ParcelService, Registry, SecretStore, ErrorLog);

        /// <summary>Stores a parcel so the pass has something to route.</summary>
        /// <param name="trackId">The tracking number.</param>
        /// <param name="trackingServiceCode">The provider code, or null.</param>
        /// <param name="paymentNumber">The payment number, defaulted from the tracking number.</param>
        /// <returns>The stored parcel.</returns>
        public Parcel InsertParcel(
            string trackId,
            string? trackingServiceCode,
            string? paymentNumber = null)
        {
            var parcel = new Parcel
            {
                PaymentNumber = paymentNumber ?? $"PAY-{trackId}",
                TrackId = trackId,
                TrackingServiceCode = trackingServiceCode,
                CreatedDatetimeUtc = FixedNow,
                IsMigratedTo1CFlag = false,
            };

            Repository.Insert(parcel);
            return parcel;
        }
    }
}
