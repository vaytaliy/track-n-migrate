using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Views;

/// <summary>
/// Main application window. Holds view-only behaviour; every rule lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isCommittingDraft;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model that backs the window.</param>
    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Clears any sort the operator applied previously so the documented default ordering is kept,
    /// which makes sorting stateless across sessions.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event arguments.</param>
    private void OnLoaded(object sender, RoutedEventArgs e) => ParcelsGrid.Items.SortDescriptions.Clear();

    /// <summary>
    /// Stops the draft row clock when the window closes.
    /// </summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The event arguments.</param>
    private void OnClosed(object? sender, EventArgs e) => _viewModel.Dispose();

    /// <summary>
    /// Selects the row that was right-clicked so the context menu acts on it.
    /// </summary>
    /// <param name="sender">The row.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRowPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            row.IsSelected = true;
        }
    }

    /// <summary>
    /// Blocks editing of every cell on a parcel that was exported to 1C.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.Item is ParcelRowViewModel row && !row.IsEditable)
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    /// Commits the draft row when the operator moves to another row, and opens the tracking number
    /// cell of the draft row as soon as it becomes current.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.OfType<ParcelRowViewModel>().Any(row => row.IsDraft))
        {
            CommitDraft();
        }

        if (!_isCommittingDraft && ParcelsGrid.SelectedItem is ParcelRowViewModel { IsDraft: true } draft)
        {
            Dispatcher.BeginInvoke(() => BeginEditDraftTrackId(draft), DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Commits the draft row when the operator presses Enter inside it.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ParcelsGrid.CurrentItem is not ParcelRowViewModel { IsDraft: true })
        {
            return;
        }

        e.Handled = true;
        CommitDraft();
    }

    /// <summary>
    /// Commits the draft row when focus leaves the grid entirely.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!ParcelsGrid.IsKeyboardFocusWithin)
        {
            CommitDraft();
        }
    }

    /// <summary>
    /// Commits pending cell edits and hands the draft row to the view model.
    /// </summary>
    private void CommitDraft()
    {
        if (_isCommittingDraft)
        {
            return;
        }

        _isCommittingDraft = true;
        try
        {
            ParcelsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ParcelsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var result = _viewModel.TryCommitDraft();
            if (result == CommitDraftResult.Blocked && _viewModel.DraftRow is { } draft)
            {
                // Keep the operator in the offending cell so the inline validation message stays visible.
                BeginEditDraftTrackId(draft);
            }
        }
        finally
        {
            _isCommittingDraft = false;
        }
    }

    /// <summary>
    /// Moves the current cell to the tracking number of the draft row and starts editing it.
    /// </summary>
    /// <param name="draft">The draft row to edit.</param>
    private void BeginEditDraftTrackId(ParcelRowViewModel draft)
    {
        var trackIdColumn = ParcelsGrid.Columns
            .FirstOrDefault(column => string.Equals(
                column.SortMemberPath,
                nameof(ParcelRowViewModel.TrackId),
                StringComparison.Ordinal));

        if (trackIdColumn is null)
        {
            return;
        }

        ParcelsGrid.CurrentCell = new DataGridCellInfo(draft, trackIdColumn);
        ParcelsGrid.BeginEdit();
        ParcelsGrid.Focus();
    }
}
