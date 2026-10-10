using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Views;

/// <summary>
/// Main application window. Holds view-only behaviour; every rule lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>
    /// Keeps the grid columns in step with the operator's stored column choice.
    /// </summary>
    private readonly GridColumnVisibilityBinder _columnVisibilityBinder;

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
        _columnVisibilityBinder = new GridColumnVisibilityBinder(ParcelsGrid, viewModel.ColumnLayout);

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
    /// Opens or closes the columns selector popover.
    /// </summary>
    /// <param name="sender">The "Колонки" button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnColumnsButtonClick(object sender, RoutedEventArgs e) =>
        ColumnsPopup.IsOpen = !ColumnsPopup.IsOpen;

    /// <summary>
    /// Sorts the saved rows through the view model instead of the platform, so the draft row can stay
    /// pinned to the bottom. Repeated clicks on one header toggle ascending and descending.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The sort request.</param>
    private void OnParcelsGridSorting(object? sender, DataGridSortingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Column.SortMemberPath))
        {
            return;
        }

        e.Handled = true;

        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        foreach (var column in ParcelsGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = direction;
        _viewModel.ApplySort(e.Column.SortMemberPath, direction);
    }

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
    /// Persists a saved row once its edit session ends, so an inline edit is no longer silently lost.
    /// The draft row is excluded because it is committed through <see cref="CommitDraft"/>.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit
            || e.Row.Item is not ParcelRowViewModel { IsDraft: false, IsEditable: true } row)
        {
            return;
        }

        // The edited values are still being committed to the row, so persist once the grid is done.
        Dispatcher.BeginInvoke(() => _viewModel.SaveRow(row), DispatcherPriority.Background);
    }

    /// <summary>
    /// Commits the draft row when the operator moves to another row, and opens the payment number
    /// cell of the draft row as soon as it becomes current.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.OfType<ParcelRowViewModel>().Any(row => row.IsDraft))
        {
            CommitDraft(refocusOnSuccess: false);
        }

        // Open the payment cell automatically when the draft row becomes current through the keyboard.
        // For a mouse click the operator chose a specific cell, so leave that choice (and its editing)
        // intact instead of redirecting the focus.
        if (!_isCommittingDraft
            && Mouse.LeftButton != MouseButtonState.Pressed
            && ParcelsGrid.SelectedItem is ParcelRowViewModel { IsDraft: true } draft)
        {
            Dispatcher.BeginInvoke(() => BeginEditDraftPaymentNumber(draft), DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Starts editing the clicked cell straight away, so an editable cell takes a single click instead of
    /// the two clicks the platform requires.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left
            || e.ClickCount != 1
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindVisualAncestor<DataGridCell>(source) is not { IsEditing: false, IsReadOnly: false } cell)
        {
            return;
        }

        // Only template columns that declare an editing template can be switched into edit mode.
        if (cell.Column is not DataGridTemplateColumn { CellEditingTemplate: not null } column
            || cell.DataContext is not ParcelRowViewModel { IsEditable: true } row)
        {
            return;
        }

        ParcelsGrid.CurrentCell = new DataGridCellInfo(row, column);
        ParcelsGrid.Focus();
        if (!ParcelsGrid.BeginEdit())
        {
            return;
        }

        ParcelsGrid.UpdateLayout();
        if (FindCellContent<TextBox>(row, column) is { } editor)
        {
            editor.Focus();
        }
    }

    /// <summary>
    /// Commits the draft row when the operator presses Enter inside it, and keeps the operator in edit mode
    /// when they move between editable cells with Tab.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnParcelsGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = TryMoveToAdjacentEditableCell(backwards: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            return;
        }

        if (e.Key != Key.Enter || ParcelsGrid.CurrentItem is not ParcelRowViewModel { IsDraft: true })
        {
            return;
        }

        e.Handled = true;
        CommitDraft(refocusOnSuccess: true);
    }

    /// <summary>
    /// Moves the current cell to the neighbouring editable text cell and opens its editor, so Tab needs a
    /// single press per field instead of one press to move and a second to start editing.
    /// </summary>
    /// <param name="backwards"><see langword="true"/> to move to the previous editable cell.</param>
    /// <returns><see langword="true"/> when focus moved; <see langword="false"/> when Tab must be left alone.</returns>
    private bool TryMoveToAdjacentEditableCell(bool backwards)
    {
        if (ParcelsGrid.CurrentCell.Item is not ParcelRowViewModel { IsEditable: true } row
            || ParcelsGrid.CurrentCell.Column is not { } currentColumn)
        {
            return false;
        }

        var editableColumns = ParcelsGrid.Columns
            .Where(column => column is DataGridTemplateColumn { CellEditingTemplate: not null })
            .ToList();

        var currentIndex = editableColumns.IndexOf(currentColumn);
        if (currentIndex < 0)
        {
            // The operator is on a read-only column; leave Tab to the platform navigation.
            return false;
        }

        var targetIndex = backwards ? currentIndex - 1 : currentIndex + 1;

        if (targetIndex < 0 || targetIndex >= editableColumns.Count)
        {
            return false;
        }

        var targetColumn = editableColumns[targetIndex];

        // The cell being left is still open, and the grid refuses to edit a second cell until it is
        // committed. Committing here is safe: the editor writes through on every keystroke.
        ParcelsGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        ParcelsGrid.CurrentCell = new DataGridCellInfo(row, targetColumn);
        ParcelsGrid.ScrollIntoView(row);
        ParcelsGrid.UpdateLayout();

        // The grid must own the focus before it will switch the cell into edit mode.
        ParcelsGrid.Focus();
        if (!ParcelsGrid.BeginEdit())
        {
            return true;
        }

        ParcelsGrid.UpdateLayout();
        if (FindCellContent<TextBox>(row, targetColumn) is { } editor)
        {
            editor.Focus();
        }

        return true;
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
            CommitDraft(refocusOnSuccess: false);
        }
    }

    /// <summary>
    /// Submits the draft row when the operator clicks the "add parcel" button.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnAddParcelClick(object sender, RoutedEventArgs e) => CommitDraft(refocusOnSuccess: true);

    /// <summary>
    /// Commits pending cell edits and hands the draft row to the view model.
    /// </summary>
    /// <param name="refocusOnSuccess">
    /// When <see langword="true"/> the tracking number cell of the fresh draft row is opened again so
    /// the operator can continue with the next parcel without reaching for the mouse.
    /// </param>
    private void CommitDraft(bool refocusOnSuccess)
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

            switch (_viewModel.TryCommitDraft())
            {
                // Keep the operator in the first required cell that still needs a value, so the inline
                // validation message stays visible and nothing else steals the focus.
                case CommitDraftResult.Blocked when _viewModel.DraftRow is { } blockedDraft:
                    FocusFirstInvalidDraftField(blockedDraft);
                    break;

                // An explicit submit of an untouched draft should still land in the first required cell
                // rather than silently doing nothing.
                case CommitDraftResult.NothingToCommit when refocusOnSuccess && _viewModel.DraftRow is { } emptyDraft:
                    BeginEditDraftPaymentNumber(emptyDraft);
                    break;

                // The draft row was replaced, so aim at the new instance on the next dispatcher turn.
                case CommitDraftResult.Succeeded when refocusOnSuccess && _viewModel.DraftRow is { } freshDraft:
                    FocusDraftPaymentNumber(freshDraft);
                    break;
            }
        }
        finally
        {
            _isCommittingDraft = false;
        }
    }

    /// <summary>
    /// Selects and scrolls to the fresh draft row, then opens its payment number cell once the
    /// collection change has been applied by the grid.
    /// </summary>
    /// <param name="draft">The draft row to focus.</param>
    private void FocusDraftPaymentNumber(ParcelRowViewModel draft)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                ParcelsGrid.SelectedItem = draft;
                ParcelsGrid.ScrollIntoView(draft);
                BeginEditDraftPaymentNumber(draft);
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// Moves focus to the first required draft cell that still has a validation error, in the order the
    /// operator is expected to fill them in (payment number, then tracking number, then provider).
    /// </summary>
    /// <param name="draft">The blocked draft row.</param>
    private void FocusFirstInvalidDraftField(ParcelRowViewModel draft)
    {
        if (HasValidationError(draft, nameof(ParcelRowViewModel.PaymentNumber)))
        {
            BeginEditDraftPaymentNumber(draft);
            return;
        }

        if (HasValidationError(draft, nameof(ParcelRowViewModel.TrackId)))
        {
            BeginEditDraftTrackId(draft);
            return;
        }

        if (HasValidationError(draft, nameof(ParcelRowViewModel.SelectedTrackingServiceCode)))
        {
            FocusDraftTrackingService(draft);
        }
    }

    /// <summary>
    /// Opens the payment number cell of the draft row, which is the first field the operator fills in.
    /// </summary>
    /// <param name="draft">The draft row to edit.</param>
    private void BeginEditDraftPaymentNumber(ParcelRowViewModel draft) =>
        BeginEditDraftCell(draft, nameof(ParcelRowViewModel.PaymentNumber));

    /// <summary>
    /// Opens the tracking number cell of the draft row.
    /// </summary>
    /// <param name="draft">The draft row to edit.</param>
    private void BeginEditDraftTrackId(ParcelRowViewModel draft) =>
        BeginEditDraftCell(draft, nameof(ParcelRowViewModel.TrackId));

    /// <summary>
    /// Moves the current cell to a text column of the draft row and puts the caret in its editor.
    /// </summary>
    /// <param name="draft">The draft row to edit.</param>
    /// <param name="sortMemberPath">The sort member path that identifies the target column.</param>
    private void BeginEditDraftCell(ParcelRowViewModel draft, string sortMemberPath)
    {
        if (FindDraftColumn(sortMemberPath) is not { } column)
        {
            return;
        }

        ParcelsGrid.CurrentCell = new DataGridCellInfo(draft, column);
        ParcelsGrid.ScrollIntoView(draft);
        ParcelsGrid.UpdateLayout();

        // The grid must own the focus before it will switch the cell into edit mode.
        ParcelsGrid.Focus();
        if (!ParcelsGrid.BeginEdit())
        {
            return;
        }

        ParcelsGrid.UpdateLayout();
        if (FindCellContent<TextBox>(draft, column) is { } editor)
        {
            editor.Focus();
            editor.SelectAll();
        }
    }

    /// <summary>
    /// Moves the current cell to the provider of the draft row and focuses its dropdown, so a missing
    /// provider is the only thing the operator has to interact with.
    /// </summary>
    /// <param name="draft">The draft row to focus.</param>
    private void FocusDraftTrackingService(ParcelRowViewModel draft)
    {
        if (FindDraftColumn(nameof(ParcelRowViewModel.TrackingServiceCode)) is not { } serviceColumn)
        {
            return;
        }

        ParcelsGrid.CurrentCell = new DataGridCellInfo(draft, serviceColumn);
        ParcelsGrid.ScrollIntoView(draft);
        ParcelsGrid.UpdateLayout();

        if (FindCellContent<ComboBox>(draft, serviceColumn) is { } providerSelector)
        {
            providerSelector.Focus();
        }
    }

    /// <summary>
    /// Finds a column of the parcel grid by the member it sorts on.
    /// </summary>
    /// <param name="sortMemberPath">The sort member path of the column to find.</param>
    /// <returns>The matching column, or <see langword="null"/> when the grid has no such column.</returns>
    private DataGridColumn? FindDraftColumn(string sortMemberPath) =>
        ParcelsGrid.Columns.FirstOrDefault(column => string.Equals(
            column.SortMemberPath,
            sortMemberPath,
            StringComparison.Ordinal));

    /// <summary>
    /// Returns the first visual child of the requested type that lives inside a realised cell.
    /// </summary>
    /// <param name="row">The row whose cell should be inspected.</param>
    /// <param name="column">The column that identifies the cell.</param>
    /// <typeparam name="TElement">The type of element to find.</typeparam>
    /// <returns>The matching element, or <see langword="null"/> when the cell or child is absent.</returns>
    private TElement? FindCellContent<TElement>(ParcelRowViewModel row, DataGridColumn column)
        where TElement : DependencyObject
    {
        if (column.GetCellContent(row) is not DependencyObject cellContent)
        {
            return null;
        }

        return FindVisualDescendant<TElement>(cellContent);
    }

    /// <summary>
    /// Walks the visual tree above an element and returns the first ancestor of the requested type.
    /// </summary>
    /// <param name="source">The element to start from.</param>
    /// <typeparam name="TElement">The type of ancestor to find.</typeparam>
    /// <returns>The matching ancestor, or <see langword="null"/> when no ancestor matches.</returns>
    private static TElement? FindVisualAncestor<TElement>(DependencyObject? source)
        where TElement : DependencyObject
    {
        while (source is not null)
        {
            if (source is TElement match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    /// <summary>
    /// Walks the visual tree below a root and returns the first descendant of the requested type.
    /// </summary>
    /// <param name="root">The visual root to search.</param>
    /// <typeparam name="TElement">The type of element to find.</typeparam>
    /// <returns>The matching element, or <see langword="null"/> when no descendant matches.</returns>
    private static TElement? FindVisualDescendant<TElement>(DependencyObject root)
        where TElement : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TElement match)
            {
                return match;
            }

            if (FindVisualDescendant<TElement>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Reports whether a draft cell currently holds a validation error.
    /// </summary>
    /// <param name="draft">The draft row to inspect.</param>
    /// <param name="propertyName">The name of the validated property.</param>
    /// <returns><see langword="true"/> when the property has at least one validation error.</returns>
    private static bool HasValidationError(ParcelRowViewModel draft, string propertyName) =>
        draft.GetErrors(propertyName).Cast<object>().Any();
}
