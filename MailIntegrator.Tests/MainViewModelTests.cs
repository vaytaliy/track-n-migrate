using System.ComponentModel;
using MailIntegrator.Data;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the main window behaviour: grid loading, the draft row lifecycle, deletion and the sync flow.
/// </summary>
public sealed class MainViewModelTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);
    private const string ProviderCode = "PochtaRussia";

    [Fact]
    public void Initialize_loads_the_stored_rows_and_appends_a_single_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-OLD", FixedNow);
        SeedParcel(sut.Repository, "RU-NEW", FixedNow.AddHours(1));

        viewModel.Initialize();

        Assert.Equal(3, viewModel.Parcels.Count);
        Assert.Equal("RU-NEW", viewModel.Parcels[0].TrackId);
        Assert.Equal("RU-OLD", viewModel.Parcels[1].TrackId);
        Assert.True(viewModel.Parcels[2].IsDraft);
        Assert.Same(viewModel.Parcels[2], viewModel.DraftRow);
    }

    [Fact]
    public void Initialize_exposes_the_registered_providers_for_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;

        viewModel.Initialize();

        Assert.Equal([ProviderCode, "DHL"], viewModel.TrackingServices.Select(service => service.Code));
        Assert.Equal(2, viewModel.DraftRow!.TrackingServices.Count);
    }

    [Fact]
    public void Initialize_numbers_the_saved_rows_in_display_order_and_skips_the_draft()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-OLD", FixedNow);
        SeedParcel(sut.Repository, "RU-NEW", FixedNow.AddHours(1));

        viewModel.Initialize();

        Assert.Equal("1", viewModel.Parcels[0].RowNumberDisplay);
        Assert.Equal("2", viewModel.Parcels[1].RowNumberDisplay);
        Assert.Equal(ParcelRowViewModel.EmptyValueDisplay, viewModel.Parcels[2].RowNumberDisplay);
    }

    [Fact]
    public void Initialize_on_an_empty_store_leaves_only_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;

        viewModel.Initialize();

        Assert.Single(viewModel.Parcels);
        Assert.True(viewModel.Parcels[0].IsDraft);
    }

    [Fact]
    public void TryCommitDraft_reports_nothing_to_commit_when_both_identifiers_are_empty()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        Assert.Equal(CommitDraftResult.NothingToCommit, viewModel.TryCommitDraft());
        Assert.Empty(sut.Repository.GetAll());
        Assert.Single(viewModel.Parcels);
    }

    [Fact]
    public void TryCommitDraft_persists_the_row_and_replaces_the_draft()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        var originalDraft = viewModel.DraftRow!;
        originalDraft.PaymentNumber = "  PAY-COMMITTED  ";
        originalDraft.TrackId = "  RU-COMMITTED  ";
        originalDraft.SelectedTrackingServiceCode = "DHL";

        var result = viewModel.TryCommitDraft();

        Assert.Equal(CommitDraftResult.Succeeded, result);

        var stored = Assert.Single(sut.Repository.GetAll());
        Assert.Equal("PAY-COMMITTED", stored.PaymentNumber);
        Assert.Equal("RU-COMMITTED", stored.TrackId);
        Assert.Equal("DHL", stored.TrackingServiceCode);
        Assert.Equal(FixedNow, stored.CreatedDatetimeUtc);
        Assert.False(stored.IsMigratedTo1CFlag);

        Assert.Equal(2, viewModel.Parcels.Count);
        Assert.Equal("PAY-COMMITTED", viewModel.Parcels[0].PaymentNumber);
        Assert.Equal("RU-COMMITTED", viewModel.Parcels[0].TrackId);
        Assert.Equal("DHL", viewModel.Parcels[0].TrackingServiceDisplay);
        Assert.False(viewModel.Parcels[0].IsDraft);
        Assert.True(viewModel.Parcels[1].IsDraft);
        Assert.NotSame(originalDraft, viewModel.DraftRow);
        Assert.DoesNotContain(originalDraft, viewModel.Parcels);
        Assert.Equal("1", viewModel.Parcels[0].RowNumberDisplay);
    }

    [Fact]
    public void TryCommitDraft_blocks_a_row_without_a_provider()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        viewModel.DraftRow!.PaymentNumber = "PAY-NO-PROVIDER";
        viewModel.DraftRow!.TrackId = "RU-NO-PROVIDER";

        var result = viewModel.TryCommitDraft();

        Assert.Equal(CommitDraftResult.Blocked, result);
        Assert.Empty(sut.Repository.GetAll());
        Assert.True(viewModel.DraftRow!.HasErrors);
    }

    [Fact]
    public void TryCommitDraft_blocks_a_duplicate_payment_number_and_keeps_the_draft()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-TAKEN", FixedNow);
        viewModel.Initialize();
        viewModel.DraftRow!.PaymentNumber = "pay-ru-taken";
        viewModel.DraftRow!.TrackId = "RU-ANY";
        viewModel.DraftRow!.SelectedTrackingServiceCode = ProviderCode;

        var result = viewModel.TryCommitDraft();

        Assert.Equal(CommitDraftResult.Blocked, result);
        Assert.Single(sut.Repository.GetAll());
        Assert.Equal(2, viewModel.Parcels.Count);
        Assert.Same(viewModel.Parcels[1], viewModel.DraftRow);
        Assert.True(viewModel.DraftRow!.HasErrors);
    }

    [Fact]
    public void TryCommitDraft_accepts_a_duplicate_tracking_number()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-SHARED", FixedNow);
        viewModel.Initialize();
        viewModel.DraftRow!.PaymentNumber = "PAY-SECOND";
        viewModel.DraftRow!.TrackId = "ru-shared";
        viewModel.DraftRow!.SelectedTrackingServiceCode = ProviderCode;

        var result = viewModel.TryCommitDraft();

        Assert.Equal(CommitDraftResult.Succeeded, result);
        Assert.Equal(2, sut.Repository.GetAll().Count);
    }

    [Fact]
    public void TryCommitDraft_places_the_new_parcel_above_older_rows()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-OLDER", FixedNow);
        viewModel.Initialize();
        sut.Clock.Advance(TimeSpan.FromHours(2));
        viewModel.DraftRow!.PaymentNumber = "PAY-NEWER";
        viewModel.DraftRow!.TrackId = "RU-NEWER";
        viewModel.DraftRow!.SelectedTrackingServiceCode = ProviderCode;

        viewModel.TryCommitDraft();

        Assert.Equal("RU-NEWER", viewModel.Parcels[0].TrackId);
        Assert.Equal("RU-OLDER", viewModel.Parcels[1].TrackId);
    }

    [Fact]
    public async Task RequestStatusesCommand_marks_the_sync_as_succeeded()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        Assert.Equal(1, sut.Sync.RunCount);
        Assert.Equal([FakeSyncService.SyncPass.Tracking], sut.Sync.RequestedPass);
        Assert.Equal(SyncState.Succeeded, viewModel.Sync.State);
        Assert.Null(viewModel.Sync.LastErrorMessage);
        Assert.False(sut.ErrorLog.HasErrors);
    }

    [Fact]
    public async Task RequestStatusesCommand_clears_the_previous_errors_before_the_pass()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        sut.ErrorLog.Report(new UserError("Почта России", "RU-OLD", "старая ошибка"));

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        Assert.False(sut.ErrorLog.HasErrors);
        Assert.Empty(sut.ErrorLog.Errors);
    }

    [Fact]
    public async Task RequestStatusesCommand_reports_a_partial_failure_in_the_footer_and_on_the_badge()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        sut.Sync.TrackingPass = _ =>
        {
            sut.ErrorLog.Report(new UserError("Почта России", "RU-BAD", "служба ответила ошибкой 503"));
            return Task.CompletedTask;
        };

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        // The pass handled the failure itself, so the badge must not claim success next to a non-empty footer.
        Assert.Equal(SyncState.Failed, viewModel.Sync.State);
        Assert.Contains("1", viewModel.Sync.LastErrorMessage, StringComparison.Ordinal);
        Assert.Single(viewModel.ErrorLog.Errors);
    }

    [Fact]
    public async Task RequestStatusesCommand_refreshes_the_grid_after_the_pass_changed_a_status()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        var seeded = SeedParcel(sut.Repository, "RU-SYNC", FixedNow);
        viewModel.Initialize();
        sut.Sync.TrackingPass = _ =>
        {
            sut.Service.ApplyTrackingResult(
                seeded.Id,
                new TrackingResult(seeded.TrackId, ParcelStatus.Delivered, FixedNow.AddHours(2)));
            return Task.CompletedTask;
        };

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        var row = viewModel.Parcels.Single(parcel => !parcel.IsDraft);
        Assert.Equal(ParcelStatus.Delivered, row.Status);
        Assert.Equal("Доставлено", row.StatusLabel);
    }

    [Fact]
    public async Task RequestStatusesCommand_keeps_the_text_typed_on_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        viewModel.DraftRow!.PaymentNumber = "PAY-PARTIAL";
        viewModel.DraftRow!.TrackId = "PARTIAL";
        viewModel.DraftRow!.SelectedTrackingServiceCode = "DHL";

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        Assert.Equal("PAY-PARTIAL", viewModel.DraftRow!.PaymentNumber);
        Assert.Equal("PARTIAL", viewModel.DraftRow!.TrackId);
        Assert.Equal("DHL", viewModel.DraftRow!.SelectedTrackingServiceCode);
    }

    [Fact]
    public async Task RequestStatusesCommand_reports_a_failure_from_the_service()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();
        sut.Sync.ExceptionToThrow = new InvalidOperationException("HTTP 503");

        await viewModel.RequestStatusesCommand.ExecuteAsync(null);

        Assert.Equal(SyncState.Failed, viewModel.Sync.State);
        Assert.Equal("HTTP 503", viewModel.Sync.LastErrorMessage);
        Assert.Equal("HTTP 503", viewModel.Sync.SymbolTooltip);

        // A failed pass must not touch parcel data.
        Assert.All(viewModel.Parcels.Where(row => !row.IsDraft), row => Assert.Null(row.Status));
    }

    [Fact]
    public async Task MigrateTo1CCommand_runs_a_migration_pass()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        await viewModel.MigrateTo1CCommand.ExecuteAsync(null);

        Assert.Equal(1, sut.Sync.RunCount);
        Assert.Equal([FakeSyncService.SyncPass.Migration], sut.Sync.RequestedPass);
        Assert.Equal(SyncState.Succeeded, viewModel.Sync.State);
    }

    [Fact]
    public void DeleteParcel_removes_the_row_after_confirmation()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-DELETE", FixedNow);
        SeedParcel(sut.Repository, "RU-KEEP", FixedNow.AddHours(1));
        viewModel.Initialize();
        sut.Dialog.ConfirmResult = true;

        viewModel.DeleteParcelCommand.Execute(viewModel.Parcels.Single(row => row.TrackId == "RU-DELETE"));

        Assert.Single(viewModel.Parcels, row => !row.IsDraft);
        Assert.Equal("RU-KEEP", sut.Repository.GetAll().Single().TrackId);
        Assert.Equal("1", viewModel.Parcels[0].RowNumberDisplay);
        Assert.Single(sut.Dialog.Confirmations);
    }

    [Fact]
    public void DeleteParcel_keeps_the_row_when_the_operator_declines()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-KEEP", FixedNow);
        viewModel.Initialize();
        sut.Dialog.ConfirmResult = false;

        viewModel.DeleteParcelCommand.Execute(viewModel.Parcels[0]);

        Assert.Single(sut.Repository.GetAll());
        Assert.Single(sut.Dialog.Confirmations);
    }

    [Fact]
    public void DeleteParcel_ignores_migrated_rows_without_prompting()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        var migrated = SeedParcel(sut.Repository, "RU-LOCKED", FixedNow);
        migrated.IsMigratedTo1CFlag = true;
        sut.Repository.Update(migrated);
        viewModel.Initialize();
        sut.Dialog.ConfirmResult = true;

        viewModel.DeleteParcelCommand.Execute(viewModel.Parcels[0]);

        Assert.Empty(sut.Dialog.Confirmations);
        Assert.Single(sut.Repository.GetAll());
    }

    [Fact]
    public void DeleteParcel_ignores_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        viewModel.DeleteParcelCommand.Execute(viewModel.DraftRow);

        Assert.Empty(sut.Dialog.Confirmations);
    }

    [Fact]
    public void OpenSettingsCommand_opens_the_settings_dialog()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, sut.Dialog.SettingsShownCount);
    }

    [Fact]
    public async Task CheckParcelStatusCommand_updates_only_the_requested_row_in_place()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-ONE", FixedNow);
        SeedParcel(sut.Repository, "RU-TWO", FixedNow.AddHours(1));
        viewModel.Initialize();
        var target = viewModel.Parcels.Single(row => row.TrackId == "RU-ONE");
        sut.Sync.SingleParcelCheck = _ =>
            new TrackingResult("RU-ONE", ParcelStatus.Delivered, FixedNow.AddHours(3));

        await viewModel.CheckParcelStatusCommand.ExecuteAsync(target);

        Assert.Equal([target.Id], sut.Sync.CheckedParcelIds);
        Assert.Equal(ParcelStatus.Delivered, target.Status);
        Assert.Equal("Доставлено", target.StatusLabel);
        Assert.Equal(SyncState.Succeeded, viewModel.Sync.State);

        // The grid keeps the same row instance and does not reload the other rows.
        Assert.Contains(target, viewModel.Parcels);
        Assert.Null(viewModel.Parcels.Single(row => row.TrackId == "RU-TWO").Status);
    }

    [Fact]
    public async Task CheckParcelStatusCommand_ignores_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        await viewModel.CheckParcelStatusCommand.ExecuteAsync(viewModel.DraftRow);

        Assert.Empty(sut.Sync.CheckedParcelIds);
        Assert.Equal(SyncState.Idle, viewModel.Sync.State);
    }

    [Fact]
    public async Task CheckParcelStatusCommand_reports_a_provider_failure_without_touching_the_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-FAIL", FixedNow);
        viewModel.Initialize();
        var row = viewModel.Parcels.Single(parcel => !parcel.IsDraft);
        sut.Sync.ExceptionToThrow = new SyncException("Не заданы учётные данные");

        await viewModel.CheckParcelStatusCommand.ExecuteAsync(row);

        Assert.Equal(SyncState.Failed, viewModel.Sync.State);
        Assert.Equal("Не заданы учётные данные", viewModel.Sync.LastErrorMessage);
        Assert.Null(row.Status);
    }

    [Fact]
    public async Task CheckParcelStatusCommand_updates_every_row_sharing_the_tracking_number()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        var requested = SeedParcel(sut.Repository, "RU-SHARED", FixedNow);
        var sibling = SeedParcel(sut.Repository, "ru-shared", FixedNow.AddMinutes(1), "PAY-SIBLING");
        SeedParcel(sut.Repository, "RU-OTHER", FixedNow.AddHours(1));
        viewModel.Initialize();
        var target = viewModel.Parcels.Single(row => row.Id == requested.Id);
        sut.Sync.SingleParcelCheck = _ =>
            new TrackingResult("RU-SHARED", ParcelStatus.Delivered, FixedNow.AddHours(3));

        await viewModel.CheckParcelStatusCommand.ExecuteAsync(target);

        Assert.Equal(ParcelStatus.Delivered, target.Status);
        Assert.Equal(ParcelStatus.Delivered, viewModel.Parcels.Single(row => row.Id == sibling.Id).Status);
        Assert.Null(viewModel.Parcels.Single(row => row.TrackId == "RU-OTHER").Status);
    }

    [Fact]
    public void SaveRow_persists_an_inline_edit_of_a_saved_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-EDIT", FixedNow);
        viewModel.Initialize();
        var row = viewModel.Parcels.Single(candidate => !candidate.IsDraft);
        row.PaymentNumber = "PAY-EDITED";
        row.TrackId = "RU-EDITED";
        row.Comment = "Принято";

        Assert.True(viewModel.SaveRow(row));

        var stored = Assert.Single(sut.Repository.GetAll());
        Assert.Equal("PAY-EDITED", stored.PaymentNumber);
        Assert.Equal("RU-EDITED", stored.TrackId);
        Assert.Equal("Принято", stored.Comment);
    }

    [Fact]
    public void SaveRow_reverts_a_duplicate_payment_number_and_reports_the_error()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-FIRST", FixedNow);
        SeedParcel(sut.Repository, "RU-SECOND", FixedNow.AddHours(1));
        viewModel.Initialize();
        var row = viewModel.Parcels.Single(candidate => candidate.TrackId == "RU-SECOND");
        row.PaymentNumber = "pay-ru-first";

        Assert.False(viewModel.SaveRow(row));

        Assert.Equal("PAY-RU-SECOND", row.PaymentNumber);
        Assert.Single(sut.Dialog.ShownErrors);
        Assert.Equal("PAY-RU-FIRST", sut.Repository.GetAll()
            .Single(parcel => parcel.TrackId == "RU-FIRST").PaymentNumber);
    }

    [Fact]
    public void SaveRow_ignores_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        viewModel.Initialize();

        Assert.False(viewModel.SaveRow(viewModel.DraftRow!));
    }

    [Fact]
    public void ApplySort_orders_the_saved_rows_and_keeps_the_draft_row_last()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-B", FixedNow, "PAY-B");
        SeedParcel(sut.Repository, "RU-A", FixedNow.AddHours(1), "PAY-A");
        SeedParcel(sut.Repository, "RU-C", FixedNow.AddHours(2), "PAY-C");
        viewModel.Initialize();

        viewModel.ApplySort(nameof(ParcelRowViewModel.PaymentNumber), ListSortDirection.Ascending);

        Assert.Equal(
            ["PAY-A", "PAY-B", "PAY-C"],
            viewModel.Parcels.Where(row => !row.IsDraft).Select(row => row.PaymentNumber));
        Assert.True(viewModel.Parcels[^1].IsDraft);
        Assert.Equal(
            ["1", "2", "3"],
            viewModel.Parcels.Where(row => !row.IsDraft).Select(row => row.RowNumberDisplay));
    }

    [Fact]
    public void ApplySort_descending_reverses_the_saved_rows_and_still_pins_the_draft_row()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-B", FixedNow, "PAY-B");
        SeedParcel(sut.Repository, "RU-A", FixedNow.AddHours(1), "PAY-A");
        SeedParcel(sut.Repository, "RU-C", FixedNow.AddHours(2), "PAY-C");
        viewModel.Initialize();

        viewModel.ApplySort(nameof(ParcelRowViewModel.PaymentNumber), ListSortDirection.Descending);

        Assert.Equal(
            ["PAY-C", "PAY-B", "PAY-A"],
            viewModel.Parcels.Where(row => !row.IsDraft).Select(row => row.PaymentNumber));
        Assert.True(viewModel.Parcels[^1].IsDraft);
    }

    [Fact]
    public void ApplySort_keeps_rows_without_a_value_last_in_both_directions()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-NONE", FixedNow, "PAY-NONE");
        var withValue = SeedParcel(sut.Repository, "RU-DONE", FixedNow.AddHours(1), "PAY-DONE");
        withValue.LastCheckedDatetimeUtc = FixedNow.AddHours(2);
        sut.Repository.Update(withValue);
        viewModel.Initialize();

        viewModel.ApplySort(nameof(ParcelRowViewModel.LastCheckedDatetimeUtc), ListSortDirection.Descending);

        Assert.Equal("PAY-DONE", viewModel.Parcels[0].PaymentNumber);
        Assert.Equal("PAY-NONE", viewModel.Parcels[1].PaymentNumber);
        Assert.True(viewModel.Parcels[^1].IsDraft);
    }

    [Fact]
    public void TryCommitDraft_under_an_active_sort_places_the_row_in_order_and_keeps_the_draft_last()
    {
        using var temp = new TempDatabase();
        var sut = CreateSut(temp);
        using var viewModel = sut.ViewModel;
        SeedParcel(sut.Repository, "RU-OLD", FixedNow, "PAY-OLD");
        viewModel.Initialize();
        viewModel.ApplySort(nameof(ParcelRowViewModel.PaymentNumber), ListSortDirection.Ascending);
        viewModel.DraftRow!.PaymentNumber = "PAY-AAA";
        viewModel.DraftRow!.TrackId = "RU-NEW";
        viewModel.DraftRow!.SelectedTrackingServiceCode = ProviderCode;

        viewModel.TryCommitDraft();

        Assert.Equal(
            ["PAY-AAA", "PAY-OLD"],
            viewModel.Parcels.Where(row => !row.IsDraft).Select(row => row.PaymentNumber));
        Assert.True(viewModel.Parcels[^1].IsDraft);
    }

    /// <summary>Builds the view model under test together with its collaborators.</summary>
    private static (
        MainViewModel ViewModel,
        ParcelService Service,
        ParcelRepository Repository,
        FakeDialogService Dialog,
        FakeSyncService Sync,
        FakeClock Clock,
        UserErrorLog ErrorLog) CreateSut(TempDatabase temp)
    {
        var repository = new ParcelRepository(temp.Database);
        var clock = new FakeClock(FixedNow);
        var dialog = new FakeDialogService();
        var sync = new FakeSyncService();
        var registry = new TrackingServiceRegistry(
        [
            new FakeTrackingService(ProviderCode, "Почта России"),
            new FakeTrackingService("DHL", "DHL"),
        ]);
        var service = new ParcelService(repository, clock, registry);
        var columnLayoutStore = new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock));
        var errorLog = new UserErrorLog();

        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            dialog,
            clock,
            new FakeLocalTimeZone(),
            registry,
            columnLayoutStore,
            errorLog);

        return (viewModel, service, repository, dialog, sync, clock, errorLog);
    }

    /// <summary>Stores a parcel directly so the grid has data to load.</summary>
    /// <param name="repository">The repository to insert into.</param>
    /// <param name="trackId">The tracking number.</param>
    /// <param name="createdUtc">The creation instant, in UTC.</param>
    /// <param name="paymentNumber">The payment number, defaulted from the tracking number.</param>
    /// <returns>The stored parcel.</returns>
    private static Parcel SeedParcel(
        IParcelRepository repository,
        string trackId,
        DateTime createdUtc,
        string? paymentNumber = null)
    {
        var parcel = new Parcel
        {
            PaymentNumber = paymentNumber ?? $"PAY-{trackId}",
            TrackId = trackId,
            TrackingServiceCode = ProviderCode,
            CreatedDatetimeUtc = createdUtc,
            IsMigratedTo1CFlag = false,
        };

        repository.Insert(parcel);
        return parcel;
    }
}
