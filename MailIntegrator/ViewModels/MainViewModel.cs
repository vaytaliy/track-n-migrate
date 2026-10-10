using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;
using MailIntegrator.Services;

namespace MailIntegrator.ViewModels;

/// <summary>
/// View model for the main window: the parcel grid, the sync status strip and the toolbar commands.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DraftClockInterval = TimeSpan.FromSeconds(1);

    private readonly IParcelService _parcelService;
    private readonly ISyncService _syncService;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;
    private readonly DispatcherTimer _draftClockTimer;

    private bool _disposed;

    /// <summary>
    /// Backing field for <see cref="DraftRow"/>.
    /// </summary>
    [ObservableProperty]
    private ParcelRowViewModel? _draftRow;

    /// <summary>
    /// Backing field for <see cref="SelectedRow"/>.
    /// </summary>
    [ObservableProperty]
    private ParcelRowViewModel? _selectedRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="parcelService">The parcel business rules.</param>
    /// <param name="syncService">The synchronisation entry point.</param>
    /// <param name="dialogService">The modal window abstraction.</param>
    /// <param name="clock">The clock used for creation timestamps and the draft row display.</param>
    public MainViewModel(
        IParcelService parcelService,
        ISyncService syncService,
        IDialogService dialogService,
        IClock clock)
    {
        _parcelService = parcelService ?? throw new ArgumentNullException(nameof(parcelService));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        Parcels = [];
        Sync = new SyncStatusViewModel();
        _draftClockTimer = new DispatcherTimer(
            DraftClockInterval,
            DispatcherPriority.Background,
            OnDraftClockTick,
            Dispatcher.CurrentDispatcher);
    }

    /// <summary>
    /// Gets the grid rows, including the trailing draft row.
    /// </summary>
    public ObservableCollection<ParcelRowViewModel> Parcels { get; }

    /// <summary>
    /// Gets the status strip state.
    /// </summary>
    public SyncStatusViewModel Sync { get; }

    /// <summary>
    /// Loads the stored parcels and starts the draft row clock.
    /// </summary>
    public void Initialize()
    {
        ReloadParcels();
        _draftClockTimer.Start();
    }

    /// <summary>
    /// Attempts to persist the draft row and replace it with a fresh draft.
    /// </summary>
    /// <returns>The outcome of the attempt, which the view uses to decide whether to keep focus.</returns>
    public CommitDraftResult TryCommitDraft()
    {
        var draft = DraftRow;
        if (draft is null || !draft.HasTrackId)
        {
            return CommitDraftResult.NothingToCommit;
        }

        if (!draft.ValidateForSave())
        {
            return CommitDraftResult.Blocked;
        }

        try
        {
            var parcel = _parcelService.CreateParcel(draft.TrackId);
            var committedRow = ParcelRowViewModel.ForExisting(parcel, _clock, IsTrackIdAvailable);

            // The default ordering is creation instant descending, so a new parcel belongs at the top.
            Parcels.Insert(0, committedRow);
            Parcels.Remove(draft);
            AppendDraftRow();
            RenumberRows();

            return CommitDraftResult.Succeeded;
        }
        catch (ParcelValidationException exception)
        {
            _dialogService.ShowError("Не удалось добавить посылку", exception.Message);
            return CommitDraftResult.Blocked;
        }
    }

    /// <summary>
    /// Releases the draft clock timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _draftClockTimer.Stop();
        _disposed = true;
    }

    /// <summary>
    /// Runs a simulated synchronisation pass for the "Запросить статус посылок" action.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    [RelayCommand]
    private Task RequestStatusesAsync(CancellationToken cancellationToken) => RunSyncAsync(cancellationToken);

    /// <summary>
    /// Runs a simulated synchronisation pass for the "Экспорт в 1С" action.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    [RelayCommand]
    private Task MigrateTo1CAsync(CancellationToken cancellationToken) => RunSyncAsync(cancellationToken);

    /// <summary>
    /// Removes a parcel that has not been exported to 1C, after confirmation.
    /// </summary>
    /// <param name="row">The row selected for deletion.</param>
    [RelayCommand]
    private void DeleteParcel(ParcelRowViewModel? row)
    {
        if (row is null || row.IsDraft || row.IsMigratedTo1CFlag)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Удаление посылки",
            $"Удалить посылку с трекинг-номером {row.TrackId}?");

        if (!confirmed)
        {
            return;
        }

        try
        {
            if (_parcelService.DeleteParcel(row.Id))
            {
                Parcels.Remove(row);
                RenumberRows();
            }
        }
        catch (ParcelValidationException exception)
        {
            _dialogService.ShowError("Не удалось удалить посылку", exception.Message);
        }
    }

    /// <summary>
    /// Opens the credential settings dialog.
    /// </summary>
    [RelayCommand]
    private void OpenSettings() => _dialogService.ShowSettings();

    /// <summary>
    /// Reloads the grid from storage and re-creates the draft row.
    /// </summary>
    private void ReloadParcels()
    {
        Parcels.Clear();

        foreach (var parcel in _parcelService.GetAllParcels())
        {
            Parcels.Add(ParcelRowViewModel.ForExisting(parcel, _clock, IsTrackIdAvailable));
        }

        AppendDraftRow();
        RenumberRows();
    }

    /// <summary>
    /// Appends a fresh draft row and keeps it at the bottom of the grid.
    /// </summary>
    private void AppendDraftRow()
    {
        var draft = ParcelRowViewModel.CreateDraft(_clock, IsTrackIdAvailable);
        DraftRow = draft;
        Parcels.Add(draft);
    }

    /// <summary>
    /// Re-numbers the saved rows so the leading column always reflects the current order.
    /// </summary>
    private void RenumberRows()
    {
        var rowNumber = 0;
        foreach (var row in Parcels)
        {
            if (row.IsDraft)
            {
                continue;
            }

            row.RowNumber = ++rowNumber;
        }
    }

    /// <summary>
    /// Reports whether a tracking number is still free, on behalf of the row view models.
    /// </summary>
    /// <param name="trackId">The candidate tracking number.</param>
    /// <param name="excludingId">An optional parcel to ignore.</param>
    /// <returns><see langword="true"/> when the tracking number can be used.</returns>
    private bool IsTrackIdAvailable(string trackId, long? excludingId) =>
        _parcelService.IsTrackIdAvailable(trackId, excludingId);

    /// <summary>
    /// Drives the status strip through a synchronisation pass.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        Sync.LastErrorMessage = null;
        Sync.State = SyncState.InProgress;

        try
        {
            await _syncService.RunAsync(cancellationToken);
            Sync.State = SyncState.Succeeded;
        }
        catch (OperationCanceledException)
        {
            Sync.State = SyncState.Idle;
        }
        catch (Exception exception)
        {
            Sync.LastErrorMessage = exception.Message;
            Sync.State = SyncState.Failed;
        }
    }

    /// <summary>
    /// Keeps the draft row's automatic creation instant current.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnDraftClockTick(object? sender, EventArgs e) => DraftRow?.RefreshDraftTimestamp();
}
