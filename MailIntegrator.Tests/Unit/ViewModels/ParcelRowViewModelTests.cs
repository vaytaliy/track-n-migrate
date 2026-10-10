using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Tests.Unit.ViewModels;

/// <summary>
/// Covers the per-row display and validation behaviour.
/// </summary>
public sealed class ParcelRowViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);

    private static readonly IReadOnlyList<TrackingServiceDescriptor> Providers =
    [
        new("PochtaRussia", "Почта России"),
        new("DHL", "DHL"),
    ];

    [Fact]
    public void Draft_row_exposes_the_expected_placeholders()
    {
        var draft = CreateDraft();

        Assert.True(draft.IsDraft);
        Assert.True(draft.IsEditable);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.RowNumberDisplay);
        Assert.True(draft.PaymentNumberCell.IsPlaceholder);
        Assert.Equal(ParcelRowViewModel.DraftPaymentNumberPlaceholder, draft.PaymentNumberCell.Text);
        Assert.True(draft.TrackIdCell.IsPlaceholder);
        Assert.Equal(ParcelRowViewModel.DraftTrackIdPlaceholder, draft.TrackIdCell.Text);
        Assert.Equal(ParcelRowViewModel.DraftTrackingServicePlaceholder, draft.TrackingServiceDisplay);
        Assert.True(draft.IsTrackingServiceMissing);
        Assert.Equal(ParcelRowViewModel.DraftCommentPlaceholder, draft.CommentCell.Text);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.SentDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.ReceivedDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.LastCheckedDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.MigratedTo1CDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, draft.StatusLabel);
        Assert.False(draft.HasLastStatus);
        Assert.Null(draft.TrackingUrl);
    }

    [Fact]
    public void Draft_row_shows_the_current_local_time()
    {
        var draft = CreateDraft();

        // 09:15:10 UTC is 12:15:10 in the test zone (UTC+3).
        Assert.Equal("03.10.2026 12:15:10", draft.CreatedDatetimeDisplay);
    }

    [Fact]
    public void Tracking_url_comes_from_the_provider_and_is_absent_when_there_is_none()
    {
        Assert.Equal(new Uri("https://tracking.test/RU1"), CreateExisting(NewParcel()).TrackingUrl);

        // The draft row has nothing to open, and so has a provider that publishes no page or is gone.
        Assert.Null(CreateDraft().TrackingUrl);

        var providerWithoutPage = NewParcel();
        providerWithoutPage.TrackingServiceCode = "DHL";
        Assert.Null(CreateExisting(providerWithoutPage).TrackingUrl);

        var unknownProvider = NewParcel();
        unknownProvider.TrackingServiceCode = "RemovedCarrier";
        Assert.Null(CreateExisting(unknownProvider).TrackingUrl);
    }

    [Fact]
    public void Changing_the_tracking_number_refreshes_the_tracking_link()
    {
        var row = CreateExisting(NewParcel());
        var changedProperties = new List<string?>();
        row.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        row.TrackId = "RU2";

        Assert.Contains(nameof(ParcelRowViewModel.TrackingUrl), changedProperties);
        Assert.Equal(new Uri("https://tracking.test/RU2"), row.TrackingUrl);
    }

    [Fact]
    public void Saved_row_renders_every_instant_in_local_time()
    {
        var row = CreateExisting(new Parcel
        {
            Id = 1,
            TrackId = "RU1",
            TrackingServiceCode = "DHL",
            CreatedDatetimeUtc = Now,
            SentDatetimeUtc = Now.AddHours(1),
            ReceivedDatetimeUtc = Now.AddHours(2),
            LastCheckedDatetimeUtc = Now.AddHours(3),
            LastStatus = ParcelStatus.InTransit,
            Comment = "Комментарий",
        });

        Assert.Equal("03.10.2026 12:15:10", row.CreatedDatetimeDisplay);
        Assert.Equal("03.10.2026 13:15:10", row.SentDatetimeDisplay);
        Assert.Equal("03.10.2026 14:15:10", row.ReceivedDatetimeDisplay);

        // The status instant is stored in UTC and displayed in local time; 12:15:10 UTC is 15:15:10.
        Assert.Equal("03.10.2026 15:15:10", row.LastCheckedDatetimeDisplay);
        Assert.Equal("Транзит", row.StatusLabel);
        Assert.Equal(ParcelStatus.InTransit, row.Status);
        Assert.Equal("DHL", row.TrackingServiceDisplay);
        Assert.False(row.IsTrackingServiceMissing);
        Assert.True(row.HasLastStatus);
    }

    [Fact]
    public void Saved_row_renders_missing_values_as_a_dash()
    {
        var row = CreateExisting(new Parcel
        {
            Id = 1,
            TrackId = "RU1",
            TrackingServiceCode = null,
            CreatedDatetimeUtc = Now,
            SentDatetimeUtc = null,
            ReceivedDatetimeUtc = null,
            LastCheckedDatetimeUtc = null,
            LastStatus = null,
            Comment = null,
        });

        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.SentDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.ReceivedDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.LastCheckedDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.MigratedTo1CDatetimeDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.StatusLabel);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.TrackingServiceDisplay);
        Assert.True(row.IsTrackingServiceMissing);
        Assert.False(row.HasLastStatus);
    }

    [Fact]
    public void Unknown_status_is_rendered_as_a_question_mark()
    {
        var parcel = NewParcel();
        parcel.LastStatus = ParcelStatus.Unknown;

        Assert.Equal("?", CreateExisting(parcel).StatusLabel);
    }

    [Fact]
    public void Saved_row_without_a_comment_shows_the_empty_placeholder()
    {
        var row = CreateExisting(NewParcel());

        Assert.True(row.CommentCell.IsPlaceholder);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, row.CommentCell.Text);
        Assert.Equal(string.Empty, row.CommentLockCaption);
    }

    [Fact]
    public void Migrated_row_is_locked_and_keeps_its_comment_read_only()
    {
        var parcel = NewParcel();
        parcel.Comment = "Завершено";
        parcel.IsMigratedTo1CFlag = true;
        parcel.MigratedTo1CDatetimeUtc = Now;

        var row = CreateExisting(parcel);

        Assert.False(row.IsEditable);
        Assert.Equal(ParcelRowViewModel.LockedCommentCaption, row.CommentLockCaption);
        Assert.False(row.CommentCell.IsPlaceholder);
        Assert.Equal("Завершено", row.CommentCell.Text);

        // The export instant is stored and displayed in UTC, hence no local shift.
        Assert.Equal("03.10.2026 09:15:10 UTC", row.MigratedTo1CDatetimeDisplay);
    }

    [Fact]
    public void Migrated_row_without_a_comment_shows_a_dash_rather_than_a_prompt()
    {
        var parcel = NewParcel();
        parcel.IsMigratedTo1CFlag = true;

        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, CreateExisting(parcel).CommentCell.Text);
    }

    [Fact]
    public void Payment_number_and_tracking_number_placeholder_flags_follow_the_entered_values()
    {
        var draft = CreateDraft();
        Assert.True(draft.PaymentNumberCell.IsPlaceholder);
        Assert.True(draft.TrackIdCell.IsPlaceholder);

        draft.PaymentNumber = "PAY-NEW";
        draft.TrackId = "RU-NEW";

        Assert.False(draft.PaymentNumberCell.IsPlaceholder);
        Assert.Equal("PAY-NEW", draft.PaymentNumberCell.Text);
        Assert.False(draft.TrackIdCell.IsPlaceholder);
        Assert.Equal("RU-NEW", draft.TrackIdCell.Text);
    }

    [Fact]
    public void Saved_row_renders_a_missing_payment_number_as_a_dash()
    {
        var parcel = NewParcel();
        parcel.PaymentNumber = string.Empty;

        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, CreateExisting(parcel).PaymentNumberCell.Text);
    }

    [Fact]
    public void Tracking_service_display_follows_the_selected_value()
    {
        var draft = CreateDraft();
        Assert.True(draft.IsTrackingServiceMissing);

        draft.SelectedTrackingServiceCode = "DHL";

        Assert.False(draft.IsTrackingServiceMissing);
        Assert.Equal("DHL", draft.TrackingServiceDisplay);
    }

    [Fact]
    public void ValidateForSave_rejects_a_missing_payment_number()
    {
        var draft = CreateDraft();
        draft.TrackId = "RU-NEW";
        draft.SelectedTrackingServiceCode = "DHL";

        Assert.False(draft.ValidateForSave());
        Assert.True(draft.HasErrors);
        Assert.NotEmpty(draft.GetErrors(nameof(ParcelRowViewModel.PaymentNumber)));
    }

    [Fact]
    public void ValidateForSave_rejects_a_missing_tracking_number()
    {
        var draft = CreateDraft();
        draft.PaymentNumber = "PAY-NEW";
        draft.SelectedTrackingServiceCode = "DHL";

        Assert.False(draft.ValidateForSave());
        Assert.True(draft.HasErrors);
        Assert.NotEmpty(draft.GetErrors(nameof(ParcelRowViewModel.TrackId)));
    }

    [Fact]
    public void ValidateForSave_rejects_a_missing_tracking_service()
    {
        var draft = CreateDraft();
        draft.PaymentNumber = "PAY-NEW";
        draft.TrackId = "RU-NEW";

        Assert.False(draft.ValidateForSave());
        Assert.True(draft.HasErrors);
        Assert.NotEmpty(draft.GetErrors(nameof(ParcelRowViewModel.SelectedTrackingServiceCode)));
    }

    [Fact]
    public void ValidateForSave_rejects_a_duplicate_payment_number()
    {
        var draft = ParcelRowViewModel.CreateDraft(
            new FakeClock(Now),
            new FakeLocalTimeZone(),
            Providers,
            (_, _) => false,
            ResolveTrackingUrl);
        draft.PaymentNumber = "PAY-DUPLICATE";
        draft.TrackId = "RU-DUPLICATE";
        draft.SelectedTrackingServiceCode = "DHL";

        Assert.False(draft.ValidateForSave());
        Assert.Contains(
            draft.GetErrors(nameof(ParcelRowViewModel.PaymentNumber)),
            error => error.ErrorMessage!.Contains("уже добавлен", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateForSave_accepts_a_free_payment_number_and_a_provider()
    {
        var draft = CreateDraft();
        draft.PaymentNumber = "PAY-FREE";
        draft.TrackId = "RU-FREE";
        draft.SelectedTrackingServiceCode = "PochtaRussia";

        Assert.True(draft.ValidateForSave());
        Assert.False(draft.HasErrors);
    }

    [Fact]
    public void Excluding_the_own_row_is_passed_to_the_availability_check()
    {
        long? capturedId = null;
        var parcel = NewParcel();
        parcel.Id = 42;

        var row = ParcelRowViewModel.ForExisting(
            parcel,
            new FakeClock(Now),
            new FakeLocalTimeZone(),
            Providers,
            (_, excludingId) =>
            {
                capturedId = excludingId;
                return true;
            },
            ResolveTrackingUrl);
        row.PaymentNumber = "PAY-OWN";

        row.ValidateForSave();

        Assert.Equal(42, capturedId);
    }

    [Fact]
    public void RefreshDraftTimestamp_raises_the_creation_display_for_the_draft_only()
    {
        var draft = CreateDraft();
        var raised = new List<string?>();
        draft.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        draft.RefreshDraftTimestamp();
        Assert.Contains(nameof(ParcelRowViewModel.CreatedDatetimeDisplay), raised);

        var existing = CreateExisting(NewParcel());
        raised.Clear();
        existing.RefreshDraftTimestamp();
        Assert.Empty(raised);
    }

    [Fact]
    public void ApplyPersistedValues_writes_through_to_the_parcel()
    {
        var row = CreateExisting(NewParcel());

        row.ApplyPersistedValues("PAY-TRIMMED", "RU-TRIMMED", "Комментарий");

        Assert.Equal("PAY-TRIMMED", row.PaymentNumber);
        Assert.Equal("RU-TRIMMED", row.TrackId);
        Assert.Equal("Комментарий", row.Comment);
        Assert.Equal("PAY-TRIMMED", row.GetParcel().PaymentNumber);
        Assert.Equal("RU-TRIMMED", row.GetParcel().TrackId);
        Assert.Equal("Комментарий", row.GetParcel().Comment);
    }

    [Fact]
    public void RevertToStoredValues_restores_the_last_saved_values()
    {
        var row = CreateExisting(NewParcel());
        row.ApplyPersistedValues("PAY-SAVED", "RU-SAVED", "Сохранено");

        row.PaymentNumber = "PAY-REJECTED";
        row.TrackId = "RU-REJECTED";
        row.Comment = "Отклонено";
        row.RevertToStoredValues();

        Assert.Equal("PAY-SAVED", row.PaymentNumber);
        Assert.Equal("RU-SAVED", row.TrackId);
        Assert.Equal("Сохранено", row.Comment);
    }

    [Fact]
    public void Row_number_display_uses_the_assigned_number()
    {
        var row = CreateExisting(NewParcel());
        row.RowNumber = 7;

        Assert.Equal("7", row.RowNumberDisplay);
    }

    [Fact]
    public void ApplyTrackingResult_refreshes_the_status_members_in_place()
    {
        var row = CreateExisting(NewParcel());

        row.ApplyTrackingResult(new TrackingResult("RU1", ParcelStatus.InTransit, Now.AddHours(2)));

        Assert.Equal(ParcelStatus.InTransit, row.Status);
        Assert.Equal("Транзит", row.StatusLabel);
        Assert.True(row.HasLastStatus);
        Assert.Equal(Now.AddHours(2), row.LastCheckedDatetimeUtc);
        Assert.Equal("03.10.2026 14:15:10", row.LastCheckedDatetimeDisplay);
    }

    [Fact]
    public void CanCheckStatus_is_false_for_the_draft_and_for_final_statuses()
    {
        Assert.False(CreateDraft().CanCheckStatus);
        Assert.True(CreateExisting(NewParcel()).CanCheckStatus);

        var inTransit = NewParcel();
        inTransit.LastStatus = ParcelStatus.InTransit;
        Assert.True(CreateExisting(inTransit).CanCheckStatus);

        var delivered = NewParcel();
        delivered.LastStatus = ParcelStatus.Delivered;
        Assert.False(CreateExisting(delivered).CanCheckStatus);

        var legacy = NewParcel();
        legacy.TrackingServiceCode = null;
        Assert.False(CreateExisting(legacy).CanCheckStatus);
    }

    /// <summary>Creates the draft row under test.</summary>
    private static ParcelRowViewModel CreateDraft() =>
        ParcelRowViewModel.CreateDraft(
            new FakeClock(Now),
            new FakeLocalTimeZone(),
            Providers,
            (_, _) => true,
            ResolveTrackingUrl);

    /// <summary>Creates a saved row under test.</summary>
    private static ParcelRowViewModel CreateExisting(Parcel parcel) =>
        ParcelRowViewModel.ForExisting(
            parcel,
            new FakeClock(Now),
            new FakeLocalTimeZone(),
            Providers,
            (_, _) => true,
            ResolveTrackingUrl);

    /// <summary>
    /// Resolves the tracking page of the test providers, mirroring the real registry: only the provider that
    /// publishes a page gets a link.
    /// </summary>
    /// <param name="trackingServiceCode">The provider code stored on the parcel.</param>
    /// <param name="trackId">The tracking number to look up.</param>
    /// <returns>The tracking page, or <see langword="null"/> for a provider without one.</returns>
    private static Uri? ResolveTrackingUrl(string trackingServiceCode, string trackId) =>
        string.Equals(trackingServiceCode, "PochtaRussia", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"https://tracking.test/{trackId}")
            : null;

    /// <summary>Creates a minimal saved parcel that already carries a provider.</summary>
    private static Parcel NewParcel() => new()
    {
        Id = 1,
        PaymentNumber = "PAY-RU1",
        TrackId = "RU1",
        TrackingServiceCode = "PochtaRussia",
        CreatedDatetimeUtc = Now,
        IsMigratedTo1CFlag = false,
    };
}
