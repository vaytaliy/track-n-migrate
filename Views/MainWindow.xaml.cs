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
            CommitDraft(refocusOnSuccess: false);
        }

        // Open the tracking cell automatically when the draft row becomes current through the keyboard.
        // For a mouse click the operator chose a specific cell, so leave that choice (and its editing)
        // intact instead of redirecting the focus.
        if (!_isCommittingDraft
            && Mouse.LeftButton != MouseButtonState.Pressed
            && ParcelsGrid.SelectedItem is ParcelRowViewModel { IsDraft: true } draft)
        {
            Dispatcher.BeginInvoke(() => BeginEditDraftTrackId(draft), DispatcherPriority.Input);
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
        CommitDraft(refocusOnSuccess: true);
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
                    BeginEditDraftTrackId(emptyDraft);
                    break;

                // The draft row was replaced, so aim at the new instance on the next dispatcher turn.
                case CommitDraftResult.Succeeded when refocusOnSuccess && _viewModel.DraftRow is { } freshDraft:
                    FocusDraftTrackId(freshDraft);
                    break;
            }
        }
        finally
        {
            _isCommittingDraft = false;
        }
    }

    /// <summary>
    /// Selects and scrolls to the fresh draft row, then opens its tracking number cell once the
    /// collection change has been applied by the grid.
    /// </summary>
    /// <param name="draft">The draft row to focus.</param>
    private void FocusDraftTrackId(ParcelRowViewModel draft)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                ParcelsGrid.SelectedItem = draft;
                ParcelsGrid.ScrollIntoView(draft);
                BeginEditDraftTrackId(draft);
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// Moves focus to the first required draft cell that still has a validation error, in the order the
    /// operator is expected to fill them in (tracking number, then provider).
    /// </summary>
    /// <param name="draft">The blocked draft row.</param>
    private void FocusFirstInvalidDraftField(ParcelRowViewModel draft)
    {
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
    /// Moves the current cell to the tracking number of the draft row and puts the caret in its editor.
    /// </summary>
    /// <param name="draft">The draft row to edit.</param>
    private void BeginEditDraftTrackId(ParcelRowViewModel draft)
    {
        if (FindDraftColumn(nameof(ParcelRowViewModel.TrackId)) is not { } trackIdColumn)
        {
            return;
        }

        ParcelsGrid.CurrentCell = new DataGridCellInfo(draft, trackIdColumn);
        ParcelsGrid.ScrollIntoView(draft);
        ParcelsGrid.UpdateLayout();

        // The grid must own the focus before it will switch the cell into edit mode.
        ParcelsGrid.Focus();
        if (!ParcelsGrid.BeginEdit())
        {
            return;
        }

        ParcelsGrid.UpdateLayout();
        if (FindCellContent<TextBox>(draft, trackIdColumn) is { } editor)
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
