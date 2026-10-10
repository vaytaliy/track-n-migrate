using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Utils;

namespace MailIntegrator.ViewModels;

/// <summary>
/// View model for the main window: the parcel grid, the sync status strip and the toolbar commands.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DraftClockInterval = TimeSpan.FromSeconds(1);

    private readonly IParcelService _parcelService;
    private readonly ITrackingSyncService _trackingSyncService;
    private readonly IMigrationSyncService _migrationSyncService;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;
    private readonly ILocalTimeZone _localTimeZone;
    private readonly ITrackingServiceRegistry _trackingServiceRegistry;
    private readonly IUserErrorLog _errorLog;
    private readonly DispatcherTimer _draftClockTimer;

    private bool _disposed;

    /// <summary>
    /// The sort member path the operator last clicked, or <see langword="null"/> for the default order.
    /// </summary>
    private string? _activeSortPath;

    /// <summary>
    /// The direction of <see cref="_activeSortPath"/>, or <see langword="null"/> when no sort is active.
    /// </summary>
    private ListSortDirection? _activeSortDirection;

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
    /// <param name="trackingSyncService">The tracking pass behind "Запросить статус посылок".</param>
    /// <param name="migrationSyncService">The migration pass behind "Экспорт в 1С".</param>
    /// <param name="dialogService">The modal window abstraction.</param>
    /// <param name="clock">The clock used for creation timestamps and the draft row display.</param>
    /// <param name="localTimeZone">The zone used to render UTC instants.</param>
    /// <param name="trackingServiceRegistry">Supplies the providers offered in the grid dropdown.</param>
    /// <param name="columnLayoutStore">Supplies the persisted set of visible grid columns.</param>
    /// <param name="errorLog">Collects the handled failures of a pass for the footer.</param>
    public MainViewModel(
        IParcelService parcelService,
        ITrackingSyncService trackingSyncService,
        IMigrationSyncService migrationSyncService,
        IDialogService dialogService,
        IClock clock,
        ILocalTimeZone localTimeZone,
        ITrackingServiceRegistry trackingServiceRegistry,
        IColumnLayoutStore columnLayoutStore,
        IUserErrorLog errorLog)
    {
        _parcelService = parcelService ?? throw new ArgumentNullException(nameof(parcelService));
        _trackingSyncService = trackingSyncService ?? throw new ArgumentNullException(nameof(trackingSyncService));
        _migrationSyncService = migrationSyncService ?? throw new ArgumentNullException(nameof(migrationSyncService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _localTimeZone = localTimeZone ?? throw new ArgumentNullException(nameof(localTimeZone));
        _trackingServiceRegistry = trackingServiceRegistry ?? throw new ArgumentNullException(nameof(trackingServiceRegistry));
        _errorLog = errorLog ?? throw new ArgumentNullException(nameof(errorLog));

        ColumnLayout = new GridColumnLayoutViewModel(columnLayoutStore);

        TrackingServices = trackingServiceRegistry.Services
            .Select(service => service.Descriptor)
            .ToList();

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
    /// Gets the tracking providers offered on the draft row, in registry order.
    /// </summary>
    public IReadOnlyList<TrackingServiceDescriptor> TrackingServices { get; }

    /// <summary>
    /// Gets the columns popover state: the grouped options and the reset action.
    /// </summary>
    public GridColumnLayoutViewModel ColumnLayout { get; }

    /// <summary>
    /// Gets the failures the footer lists: emptied at the start of every pass and repopulated with the
    /// failures that pass handled.
    /// </summary>
    public IUserErrorLog ErrorLog => _errorLog;

    /// <summary>
    /// Loads the stored parcels and starts the draft row clock.
    /// </summary>
    public void Initialize()
    {
        ReloadParcels();
        _draftClockTimer.Start();
    }

    /// <summary>
    /// Sorts the saved grid rows by a column, keeping the draft row pinned to the bottom.
    /// </summary>
    /// <param name="sortMemberPath">The <c>SortMemberPath</c> of the clicked column.</param>
    /// <param name="direction">The direction the operator asked for.</param>
    public void ApplySort(string sortMemberPath, ListSortDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sortMemberPath);

        _activeSortPath = sortMemberPath;
        _activeSortDirection = direction;
        ReorderRows();
    }

    /// <summary>
    /// Attempts to persist the draft row and replace it with a fresh draft.
    /// </summary>
    /// <returns>The outcome of the attempt, which the view uses to decide whether to keep focus.</returns>
    public CommitDraftResult TryCommitDraft()
    {
        var draft = DraftRow;
        if (draft is null
            || (TextField.IsEmpty(draft.PaymentNumber) && TextField.IsEmpty(draft.TrackId)))
        {
            return CommitDraftResult.NothingToCommit;
        }

        if (!draft.ValidateForSave())
        {
            return CommitDraftResult.Blocked;
        }

        try
        {
            var parcel = _parcelService.CreateParcel(
                draft.PaymentNumber,
                draft.TrackId,
                draft.SelectedTrackingServiceCode);
            var committedRow = CreateRow(parcel);

            // The default ordering is creation instant descending, so a new parcel belongs at the top.
            Parcels.Insert(0, committedRow);
            Parcels.Remove(draft);
            AppendDraftRow();
            ReorderRows();

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
    /// Runs a tracking pass for the "Запросить статус посылок" action.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    [RelayCommand]
    private Task RequestStatusesAsync(CancellationToken cancellationToken) =>
        RunPassAsync(_trackingSyncService.RunAsync, ReloadParcels, cancellationToken);

    /// <summary>
    /// Runs a migration pass for the "Экспорт в 1С" action.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    [RelayCommand]
    private Task MigrateTo1CAsync(CancellationToken cancellationToken) =>
        RunPassAsync(_migrationSyncService.RunAsync, onSuccess: null, cancellationToken);

    /// <summary>
    /// Polls the single parcel behind a grid row and refreshes only that row.
    /// </summary>
    /// <param name="row">The row the operator selected in the context menu.</param>
    [RelayCommand]
    private Task CheckParcelStatusAsync(ParcelRowViewModel? row)
    {
        if (row is null || row.IsDraft)
        {
            return Task.CompletedTask;
        }

        return RunPassAsync(
            async cancellationToken =>
            {
                var result = await _trackingSyncService.RunForParcelAsync(row.Id, cancellationToken);
                if (result is null)
                {
                    return;
                }

                // The rows wrap their own parcel instances, so they are updated in place: the grid keeps
                // its selection and does not need the full reload the bulk pass performs. Every row that
                // shares the polled tracking number receives the same answer.
                foreach (var matchingRow in Parcels.Where(candidate => SharesTrackId(candidate, row)))
                {
                    matchingRow.ApplyTrackingResult(result);
                }
            },
            onSuccess: null,
            CancellationToken.None);
    }

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
            $"Удалить посылку с номером счёта {row.PaymentNumber}?");

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
    /// Persists an inline edit of a saved row, so the grid and storage stay in step.
    /// </summary>
    /// <param name="row">The row whose editor just closed.</param>
    /// <returns><see langword="true"/> when the edit was persisted.</returns>
    /// <remarks>
    /// A rejected edit is reported and then reverted to the last stored values, so the operator never sees
    /// a value that was not persisted. The draft row is not handled here; it is committed as a whole.
    /// </remarks>
    public bool SaveRow(ParcelRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.IsDraft || !row.IsEditable)
        {
            return false;
        }

        if (!row.ValidateForSave())
        {
            _dialogService.ShowError("Не удалось сохранить посылку", DescribeFirstError(row));
            row.RevertToStoredValues();
            return false;
        }

        try
        {
            var parcel = _parcelService.UpdateEditableFields(
                row.Id,
                row.PaymentNumber,
                row.TrackId,
                row.Comment);

            row.ApplyPersistedValues(parcel.PaymentNumber, parcel.TrackId, parcel.Comment);
            return true;
        }
        catch (ParcelValidationException exception)
        {
            _dialogService.ShowError("Не удалось сохранить посылку", exception.Message);
            row.RevertToStoredValues();
            return false;
        }
    }

    /// <summary>
    /// Picks the first validation message of a row so a rejected inline edit can be explained.
    /// </summary>
    /// <param name="row">The row that failed validation.</param>
    /// <returns>The first error message, or a generic fallback.</returns>
    private static string DescribeFirstError(ParcelRowViewModel row) =>
        row.GetErrors(propertyName: null)
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
        ?? "Проверьте заполненные поля.";

    /// <summary>
    /// Reports whether a row belongs to the same provider and tracking number as the polled row, which is
    /// what makes the two rows share one status.
    /// </summary>
    /// <param name="candidate">The row to compare.</param>
    /// <param name="row">The row that was polled.</param>
    /// <returns><see langword="true"/> when both rows carry the same tracking identity.</returns>
    private static bool SharesTrackId(ParcelRowViewModel candidate, ParcelRowViewModel row) =>
        !candidate.IsDraft
        && string.Equals(
            candidate.TrackingServiceCode,
            row.TrackingServiceCode,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(candidate.TrackId, row.TrackId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens the credential settings dialog.
    /// </summary>
    [RelayCommand]
    private void OpenSettings() => _dialogService.ShowSettings();

    /// <summary>
    /// Builds a row view model for a persisted parcel.
    /// </summary>
    /// <param name="parcel">The persisted parcel.</param>
    /// <returns>The row view model.</returns>
    private ParcelRowViewModel CreateRow(Parcel parcel) =>
        ParcelRowViewModel.ForExisting(
            parcel,
            _clock,
            _localTimeZone,
            TrackingServices,
            IsPaymentNumberAvailable,
            ResolveTrackingUrl);

    /// <summary>
    /// Reloads the grid from storage, keeping any text the operator already typed on the draft row.
    /// </summary>
    private void ReloadParcels()
    {
        var pendingPaymentNumber = DraftRow?.PaymentNumber ?? string.Empty;
        var pendingTrackId = DraftRow?.TrackId ?? string.Empty;
        var pendingProvider = DraftRow?.SelectedTrackingServiceCode ?? string.Empty;

        Parcels.Clear();

        foreach (var parcel in _parcelService.GetAllParcels())
        {
            Parcels.Add(CreateRow(parcel));
        }

        AppendDraftRow();

        if (!TextField.IsEmpty(pendingPaymentNumber)
            || !TextField.IsEmpty(pendingTrackId)
            || !TextField.IsEmpty(pendingProvider))
        {
            DraftRow!.PaymentNumber = pendingPaymentNumber;
            DraftRow!.TrackId = pendingTrackId;
            DraftRow!.SelectedTrackingServiceCode = pendingProvider;
        }

        ReorderRows();
    }

    /// <summary>
    /// Appends a fresh draft row and keeps it at the bottom of the grid.
    /// </summary>
    private void AppendDraftRow()
    {
        var draft = ParcelRowViewModel.CreateDraft(
            _clock,
            _localTimeZone,
            TrackingServices,
            IsPaymentNumberAvailable,
            ResolveTrackingUrl);

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
    /// Applies the active sort to the saved rows, moves the draft row to the bottom and re-numbers.
    /// </summary>
    /// <remarks>
    /// The draft row is excluded from the sort on purpose: it is the add-new-record row, so it must stay
    /// last whatever the operator sorts by.
    /// </remarks>
    private void ReorderRows()
    {
        if (_activeSortPath is null || _activeSortDirection is null)
        {
            EnsureDraftRowIsLast();
            RenumberRows();
            return;
        }

        var sortedRows = Parcels.Where(row => !row.IsDraft).ToList();
        sortedRows.Sort(CompareByActiveSort);

        MoveSavedRowsToOrder(sortedRows);
        EnsureDraftRowIsLast();
        RenumberRows();
    }

    /// <summary>
    /// Compares two saved rows by the active sort member, honouring the active direction and keeping
    /// rows without a value last in both directions.
    /// </summary>
    /// <param name="left">The first row.</param>
    /// <param name="right">The second row.</param>
    /// <returns>A negative, zero or positive value, as required by <see cref="List{T}.Sort(Comparison{T})"/>.</returns>
    private int CompareByActiveSort(ParcelRowViewModel left, ParcelRowViewModel right)
    {
        var leftKey = GetSortKey(left, _activeSortPath!);
        var rightKey = GetSortKey(right, _activeSortPath!);

        // Rows without a value stay last in both directions, so the null check is not negated below.
        if (leftKey is null)
        {
            return rightKey is null ? 0 : 1;
        }

        if (rightKey is null)
        {
            return -1;
        }

        var comparison = Comparer<object>.Default.Compare(leftKey, rightKey);
        return _activeSortDirection == ListSortDirection.Descending ? -comparison : comparison;
    }

    /// <summary>
    /// Reads the value of a row that the given column sorts on.
    /// </summary>
    /// <param name="row">The row to read.</param>
    /// <param name="sortMemberPath">The column's sort member path.</param>
    /// <returns>The boxed value, or <see langword="null"/> when the column is not sortable.</returns>
    private static object? GetSortKey(ParcelRowViewModel row, string sortMemberPath) => sortMemberPath switch
    {
        nameof(ParcelRowViewModel.RowNumber) => row.RowNumber,
        nameof(ParcelRowViewModel.CreatedDatetimeUtc) => row.CreatedDatetimeUtc,
        nameof(ParcelRowViewModel.PaymentNumber) => row.PaymentNumber,
        nameof(ParcelRowViewModel.TrackId) => row.TrackId,
        nameof(ParcelRowViewModel.TrackingServiceCode) => row.TrackingServiceCode,
        nameof(ParcelRowViewModel.SentDatetimeUtc) => row.SentDatetimeUtc,
        nameof(ParcelRowViewModel.ReceivedDatetimeUtc) => row.ReceivedDatetimeUtc,
        nameof(ParcelRowViewModel.LastCheckedDatetimeUtc) => row.LastCheckedDatetimeUtc,
        nameof(ParcelRowViewModel.Status) => row.Status,
        nameof(ParcelRowViewModel.Comment) => row.Comment,
        nameof(ParcelRowViewModel.IsMigratedTo1CFlag) => row.IsMigratedTo1CFlag,
        nameof(ParcelRowViewModel.MigratedTo1CDatetimeUtc) => row.MigratedTo1CDatetimeUtc,
        _ => null,
    };

    /// <summary>
    /// Reorders the saved rows to the requested order using move operations, so the grid keeps its row
    /// instances and selection instead of rebuilding from scratch.
    /// </summary>
    /// <param name="orderedRows">The saved rows in their target order.</param>
    private void MoveSavedRowsToOrder(IReadOnlyList<ParcelRowViewModel> orderedRows)
    {
        for (var targetIndex = 0; targetIndex < orderedRows.Count; targetIndex++)
        {
            var currentIndex = Parcels.IndexOf(orderedRows[targetIndex]);
            if (currentIndex != targetIndex)
            {
                Parcels.Move(currentIndex, targetIndex);
            }
        }
    }

    /// <summary>
    /// Moves the draft row to the end of the grid when it is not already there.
    /// </summary>
    private void EnsureDraftRowIsLast()
    {
        if (DraftRow is not { } draft)
        {
            return;
        }

        var currentIndex = Parcels.IndexOf(draft);
        if (currentIndex >= 0 && currentIndex != Parcels.Count - 1)
        {
            Parcels.Move(currentIndex, Parcels.Count - 1);
        }
    }

    /// <summary>
    /// Reports whether a payment number is still free, on behalf of the row view models.
    /// </summary>
    /// <param name="paymentNumber">The candidate payment number.</param>
    /// <param name="excludingId">An optional parcel to ignore.</param>
    /// <returns><see langword="true"/> when the payment number can be used.</returns>
    private bool IsPaymentNumberAvailable(string paymentNumber, long? excludingId) =>
        _parcelService.IsPaymentNumberAvailable(paymentNumber, excludingId);

    /// <summary>
    /// Builds the public tracking page of a parcel on behalf of the row view models.
    /// </summary>
    /// <param name="trackingServiceCode">The provider code stored on the parcel.</param>
    /// <param name="trackId">The tracking number to look up.</param>
    /// <returns>The provider's tracking page, or <see langword="null"/> when the provider has none.</returns>
    private Uri? ResolveTrackingUrl(string trackingServiceCode, string trackId) =>
        _trackingServiceRegistry.FindByCode(trackingServiceCode)?.GetTrackingUrl(trackId);

    /// <summary>
    /// Drives the status strip through one synchronisation pass.
    /// </summary>
    /// <param name="pass">The pass to execute.</param>
    /// <param name="onSuccess">
    /// An optional action that reflects a successful pass in the grid: a full reload for the bulk tracking
    /// pass, or <see langword="null"/> when the pass already updated the affected rows itself.
    /// </param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    private async Task RunPassAsync(
        Func<CancellationToken, Task> pass,
        Action? onSuccess,
        CancellationToken cancellationToken)
    {
        Sync.LastErrorMessage = null;
        _errorLog.BeginPass();
        Sync.State = SyncState.InProgress;

        try
        {
            await pass(cancellationToken);

            // A pass that handled some failures still refreshed the rows it could reach.
            onSuccess?.Invoke();

            if (_errorLog.HasErrors)
            {
                Sync.LastErrorMessage = $"Не удалось обработать часть посылок: {_errorLog.Errors.Count}.";
                Sync.State = SyncState.Failed;
                return;
            }

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
