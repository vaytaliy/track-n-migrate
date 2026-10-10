using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MailIntegrator.Data;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;
using MailIntegrator.ViewModels;
using MailIntegrator.Views;
using MailIntegrator.Views.Controls;

namespace MailIntegrator.Tests;

/// <summary>
/// Loads the real XAML off-screen. This is the only test that can catch missing resource keys, broken
/// bindings and template failures, because those surface at run time rather than at build time.
/// </summary>
/// <remarks>
/// <see cref="Application"/> is thread affine and only one may exist per process, so every window is
/// rendered on a single dedicated STA thread inside one test.
/// </remarks>
public sealed class XamlSmokeTests
{
    [Fact]
    public void Windows_load_and_render_without_errors()
    {
        RunOnStaThread(() =>
        {
            EnsureApplicationLoaded();

            RenderMainWindow();
            RenderSettingsWindow();
            SubmitDraft_and_focus_the_required_cell();
            Single_click_edits_an_editable_cell();
            Service_dropdown_shows_the_provider_label();
            Tab_opens_the_next_editable_cell_in_edit_mode();
        });
    }

    /// <summary>Renders the main window with data and walks the status strip through every state.</summary>
    private static void RenderMainWindow()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var now = new DateTime(2026, 10, 3, 9, 15, 10, DateTimeKind.Utc);

        repository.Insert(new Parcel
        {
            PaymentNumber = "PAY-9842104921",
            TrackId = "RU9842104921CN",
            TrackingServiceCode = "PochtaRussia",
            CreatedDatetimeUtc = now,
            SentDatetimeUtc = now.AddHours(1),
            ReceivedDatetimeUtc = now.AddHours(2),
            LastCheckedDatetimeUtc = now.AddHours(3),
            LastStatus = ParcelStatus.Delivered,
            Comment = "Завершено",
            IsMigratedTo1CFlag = true,
            MigratedTo1CDatetimeUtc = now.AddDays(1),
        });

        repository.Insert(new Parcel
        {
            PaymentNumber = "PAY-4729103852",
            TrackId = "RU4729103852CN",
            TrackingServiceCode = "DHL",
            CreatedDatetimeUtc = now.AddDays(1),
            LastCheckedDatetimeUtc = now.AddHours(4),
            LastStatus = ParcelStatus.Processing,
            IsMigratedTo1CFlag = false,
        });

        var clock = new SystemClock();
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var service = new ParcelService(repository, clock, registry);
        var sync = new FakeSyncService();
        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            new FakeDialogService(),
            clock,
            new SystemLocalTimeZone(),
            registry,
            new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock)),
            new UserErrorLog());

        try
        {
            viewModel.Initialize();

            var window = CreateOffScreenWindow(new MainWindow(viewModel));
            try
            {
                window.Show();
                window.UpdateLayout();

                var grid = Assert.IsType<DataGrid>(window.FindName("ParcelsGrid"));
                Assert.Equal(3, grid.Items.Count);
                AssertDraftRowExposesTheAddButton(grid);

                // The columns popover is built with the window, so its styles and templates load here.
                var columnsPopup = Assert.IsType<Popup>(window.FindName("ColumnsPopup"));
                Assert.IsType<ColumnsPopover>(columnsPopup.Child);

                // Toggling a column in the popover is reflected on the grid immediately.
                var commentColumn = grid.Columns.Single(column => column.SortMemberPath == "Comment");
                viewModel.ColumnLayout.AllColumns.Single(option => option.Key == "Comment").IsVisible = false;
                window.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, commentColumn.Visibility);

                // Showing every column forces horizontal overflow, and the bottom scrollbar must span the
                // grid rather than collapsing to a point (the dark scrollbar style used to be vertical-only).
                viewModel.ColumnLayout.AllColumns.ToList().ForEach(option => option.IsVisible = true);
                window.UpdateLayout();
                var horizontalScrollBar = FindVisualDescendants<ScrollBar>(grid)
                    .Single(bar => bar.Orientation == Orientation.Horizontal);
                Assert.Equal(Visibility.Visible, horizontalScrollBar.Visibility);
                Assert.True(
                    horizontalScrollBar.ActualWidth > grid.ActualWidth / 2,
                    $"Expected the horizontal scrollbar to span the grid, but it was {horizontalScrollBar.ActualWidth} wide.");

                // Sorting must keep the add-new-record draft row pinned to the bottom.
                viewModel.ApplySort(nameof(ParcelRowViewModel.PaymentNumber), ListSortDirection.Ascending);
                window.UpdateLayout();
                var lastRow = Assert.IsType<ParcelRowViewModel>(grid.Items[grid.Items.Count - 1]);
                Assert.True(lastRow.IsDraft);

                // The edit pencil moved from every cell into the three editable column headers.
                var pencilHeaderTemplate = Assert.IsType<DataTemplate>(
                    Application.Current.FindResource("EditableColumnHeaderTemplate"));
                foreach (var key in new[] { "PaymentNumber", "TrackId", "Comment" })
                {
                    var editableColumn = grid.Columns.Single(candidate => candidate.SortMemberPath == key);
                    Assert.Same(pencilHeaderTemplate, editableColumn.HeaderTemplate);

                    // The header really renders the text plus the pencil glyph, not just the template reference.
                    var header = FindVisualDescendants<DataGridColumnHeader>(grid)
                        .Single(candidate => ReferenceEquals(candidate.Column, editableColumn));
                    Assert.Contains(
                        FindVisualDescendants<TextBlock>(header),
                        block => block.Text == (string)editableColumn.Header!);
                    Assert.NotEmpty(FindVisualDescendants<Path>(header));
                }

                // The status column is flat text now: no badge, no fill, no icon, only the font colour.
                var statusCells = FindVisualDescendants<DataGridCell>(grid)
                    .Where(cell => cell.Column?.SortMemberPath == "Status")
                    .ToList();
                Assert.NotEmpty(statusCells);
                Assert.All(
                    statusCells,
                    cell => Assert.Empty(FindVisualDescendants<Path>(cell)));
                var deliveredLabel = statusCells
                    .SelectMany(FindVisualDescendants<TextBlock>)
                    .Single(block => block.Text == "Доставлено");
                Assert.Same(Application.Current.FindResource("SuccessBrush"), deliveredLabel.Foreground);

                // The tracking link exists exactly for the provider that publishes a page.
                var editableCells = FindVisualDescendants<EditableTextCell>(grid).ToList();
                Assert.Contains(
                    editableCells,
                    cell => cell.LinkUrl is not null
                        && cell.DataContext is ParcelRowViewModel { TrackId: "RU9842104921CN" });
                Assert.DoesNotContain(
                    editableCells,
                    cell => cell.LinkUrl is not null
                        && cell.DataContext is ParcelRowViewModel { TrackId: "RU4729103852CN" });

                // The footer is a fixed-height band and renders every message it is given.
                var footer = Assert.IsType<StatusFooter>(window.FindName("FailureFooter"));
                Assert.Equal(48, footer.Height);
                Assert.Empty(footer.Messages!.Cast<object>());
                viewModel.ErrorLog.Report(new UserError("Почта России", "RU-BAD", "служба ответила ошибкой 503"));
                window.UpdateLayout();
                Assert.Contains(
                    FindVisualDescendants<TextBlock>(footer).Select(block => block.Text),
                    text => text.Contains("RU-BAD", StringComparison.Ordinal));

                // Walk the status strip through every state so its triggers and templates are evaluated.
                foreach (var state in new[] { SyncState.InProgress, SyncState.Succeeded, SyncState.Failed })
                {
                    viewModel.Sync.LastErrorMessage = state == SyncState.Failed ? "Демонстрация" : null;
                    viewModel.Sync.State = state;
                    window.UpdateLayout();
                }
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>Renders the credential dialog with one editor per provider plus the 1C target.</summary>
    private static void RenderSettingsWindow()
    {
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var window = CreateOffScreenWindow(
            new SettingsWindow(new SettingsViewModel(registry, new FakeSecretStore())));
        var expectedEditors = registry.Services.Count + 1;

        try
        {
            window.Show();
            window.UpdateLayout();

            var editors = FindDescendant<ItemsControl>(window);
            Assert.NotNull(editors);
            Assert.Equal(expectedEditors, editors!.Items.Count);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Submits the draft through the grid button and verifies where the caret lands: the fresh draft's
    /// tracking cell after success, and only the first required cell when validation blocks the save.
    /// </summary>
    private static void SubmitDraft_and_focus_the_required_cell()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var clock = new SystemClock();
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var service = new ParcelService(repository, clock, registry);
        var sync = new FakeSyncService();
        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            new FakeDialogService(),
            clock,
            new SystemLocalTimeZone(),
            registry,
            new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock)),
            new UserErrorLog());

        try
        {
            viewModel.Initialize();

            var window = CreateOffScreenWindow(new MainWindow(viewModel));
            try
            {
                window.Show();
                window.UpdateLayout();
                var grid = Assert.IsType<DataGrid>(window.FindName("ParcelsGrid"));

                // A complete draft commits and the fresh draft's payment number cell becomes current.
                var draft = viewModel.DraftRow!;
                draft.PaymentNumber = "PAY-FOCUS-1";
                draft.TrackId = "RU-FOCUS-1";
                draft.SelectedTrackingServiceCode = "DHL";
                ClickAddButton(grid);
                FlushDispatcher(window);

                var freshDraft = viewModel.DraftRow!;
                Assert.True(freshDraft.IsDraft);
                Assert.Same(freshDraft, grid.CurrentCell.Item);
                Assert.Equal(nameof(ParcelRowViewModel.PaymentNumber), grid.CurrentCell.Column?.SortMemberPath);

                // A missing provider focuses the provider cell, not the already-filled identifier cells.
                freshDraft.PaymentNumber = "PAY-FOCUS-2";
                freshDraft.TrackId = "RU-FOCUS-2";
                freshDraft.SelectedTrackingServiceCode = string.Empty;
                ClickAddButton(grid);
                Assert.Same(freshDraft, grid.CurrentCell.Item);
                Assert.Equal(
                    nameof(ParcelRowViewModel.TrackingServiceCode),
                    grid.CurrentCell.Column?.SortMemberPath);

                // A missing tracking number focuses the tracking cell even when the provider is set.
                freshDraft.TrackId = string.Empty;
                freshDraft.SelectedTrackingServiceCode = "DHL";
                ClickAddButton(grid);
                Assert.Same(freshDraft, grid.CurrentCell.Item);
                Assert.Equal(nameof(ParcelRowViewModel.TrackId), grid.CurrentCell.Column?.SortMemberPath);

                // A missing payment number focuses the payment cell, which is the first field to fill in.
                freshDraft.PaymentNumber = string.Empty;
                freshDraft.TrackId = "RU-FOCUS-4";
                ClickAddButton(grid);
                Assert.Same(freshDraft, grid.CurrentCell.Item);
                Assert.Equal(nameof(ParcelRowViewModel.PaymentNumber), grid.CurrentCell.Column?.SortMemberPath);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies that one left-click on an editable template cell switches it into edit mode, which is the
    /// behaviour the comment and tracking cells rely on.
    /// </summary>
    private static void Single_click_edits_an_editable_cell()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        repository.Insert(new Parcel
        {
            PaymentNumber = "PAY-CLICK",
            TrackId = "RU-CLICK",
            TrackingServiceCode = "DHL",
            CreatedDatetimeUtc = new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc),
            IsMigratedTo1CFlag = false,
        });

        var clock = new SystemClock();
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var service = new ParcelService(repository, clock, registry);
        var sync = new FakeSyncService();
        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            new FakeDialogService(),
            clock,
            new SystemLocalTimeZone(),
            registry,
            new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock)),
            new UserErrorLog());

        try
        {
            viewModel.Initialize();

            var window = CreateOffScreenWindow(new MainWindow(viewModel));
            try
            {
                window.Show();
                window.Activate();
                window.UpdateLayout();
                var grid = Assert.IsType<DataGrid>(window.FindName("ParcelsGrid"));
                grid.Focus();

                var commentCell = FindVisualDescendants<DataGridCell>(grid).Single(
                    cell => Equals(cell.Column?.Header, "Комментарий")
                        && cell.DataContext is ParcelRowViewModel { IsDraft: false });

                Assert.False(commentCell.IsEditing);
                RaiseSingleLeftClick(commentCell);
                Assert.Same(commentCell.Column, grid.CurrentCell.Column);
                Assert.Same(commentCell.DataContext, grid.CurrentCell.Item);
                Assert.True(commentCell.IsEditing);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the provider dropdown shows the provider's display name rather than the descriptor's
    /// <c>ToString()</c> output, both for the selection box and for the dropdown items.
    /// </summary>
    private static void Service_dropdown_shows_the_provider_label()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var clock = new SystemClock();
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var service = new ParcelService(repository, clock, registry);
        var sync = new FakeSyncService();
        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            new FakeDialogService(),
            clock,
            new SystemLocalTimeZone(),
            registry,
            new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock)),
            new UserErrorLog());

        try
        {
            viewModel.Initialize();

            var window = CreateOffScreenWindow(new MainWindow(viewModel));
            try
            {
                window.Show();
                window.UpdateLayout();
                var grid = Assert.IsType<DataGrid>(window.FindName("ParcelsGrid"));
                var providerSelector = FindVisualDescendants<ComboBox>(grid)
                    .Single(box => box.DataContext is ParcelRowViewModel { IsDraft: true });

                viewModel.DraftRow!.SelectedTrackingServiceCode = "PochtaRussia";
                window.UpdateLayout();

                var renderedTexts = FindVisualDescendants<TextBlock>(providerSelector)
                    .Select(textBlock => textBlock.Text)
                    .ToList();

                // Selection box.
                Assert.Contains(renderedTexts, text => text.Contains("Почта России", StringComparison.Ordinal));
                Assert.DoesNotContain(
                    renderedTexts,
                    text => text.Contains("TrackingServiceDescriptor", StringComparison.Ordinal));

                // Dropdown items use the same item template, so the realised containers must match too.
                providerSelector.IsDropDownOpen = true;
                providerSelector.UpdateLayout();
                for (var index = 0; index < providerSelector.Items.Count; index++)
                {
                    if (providerSelector.ItemContainerGenerator.ContainerFromIndex(index) is not ComboBoxItem item)
                    {
                        continue;
                    }

                    var itemTexts = FindVisualDescendants<TextBlock>(item).Select(textBlock => textBlock.Text);
                    Assert.DoesNotContain(
                        itemTexts,
                        text => text.Contains("TrackingServiceDescriptor", StringComparison.Ordinal));
                }
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>
    /// Verifies that Tab moves from one editable cell to the next and leaves the new cell in edit mode,
    /// instead of only moving the selection and requiring a second press.
    /// </summary>
    private static void Tab_opens_the_next_editable_cell_in_edit_mode()
    {
        using var temp = new TempDatabase();
        var repository = new ParcelRepository(temp.Database);
        var clock = new SystemClock();
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var service = new ParcelService(repository, clock, registry);
        var sync = new FakeSyncService();
        var viewModel = new MainViewModel(
            service,
            sync,
            sync,
            new FakeDialogService(),
            clock,
            new SystemLocalTimeZone(),
            registry,
            new ColumnLayoutStore(new AppSettingsRepository(temp.Database, clock)),
            new UserErrorLog());

        try
        {
            viewModel.Initialize();

            var window = CreateOffScreenWindow(new MainWindow(viewModel));
            try
            {
                window.Show();
                window.Activate();
                window.UpdateLayout();
                var grid = Assert.IsType<DataGrid>(window.FindName("ParcelsGrid"));
                var draft = viewModel.DraftRow!;

                // Open the payment cell the way the window does when the draft row becomes current.
                var paymentColumn = grid.Columns.Single(
                    column => column.SortMemberPath == nameof(ParcelRowViewModel.PaymentNumber));
                grid.CurrentCell = new DataGridCellInfo(draft, paymentColumn);
                grid.ScrollIntoView(draft);
                grid.UpdateLayout();
                grid.Focus();
                Assert.True(grid.BeginEdit());
                FlushDispatcher(window);

                grid.RaiseEvent(new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(grid),
                    timestamp: 0,
                    Key.Tab)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                });
                FlushDispatcher(window);

                var trackColumn = grid.Columns.Single(
                    column => column.SortMemberPath == nameof(ParcelRowViewModel.TrackId));
                Assert.Equal(nameof(ParcelRowViewModel.TrackId), grid.CurrentCell.Column?.SortMemberPath);
                Assert.Same(draft, grid.CurrentCell.Item);

                var trackCell = FindVisualDescendants<DataGridCell>(grid).Single(
                    cell => ReferenceEquals(cell.Column, trackColumn)
                        && ReferenceEquals(cell.DataContext, draft));
                Assert.True(trackCell.IsEditing);
                Assert.NotEmpty(FindVisualDescendants<TextBox>(trackCell));
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    /// <summary>Raises one tunnelling left-button click on an element.</summary>
    private static void RaiseSingleLeftClick(UIElement target)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseDownEvent,
        };

        target.RaiseEvent(args);
    }

    /// <summary>Clicks the visible add button of the draft row.</summary>
    private static void ClickAddButton(DataGrid grid)
    {
        var button = FindVisualDescendants<Button>(grid).Single(
            candidate => Equals(candidate.Tag, "DraftAddButton") && candidate.Visibility == Visibility.Visible);

        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }

    /// <summary>Lets every queued dispatcher callback up to the idle level run.</summary>
    private static void FlushDispatcher(Window window) =>
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>
    /// Verifies that the draft row renders the explicit add button and that saved rows do not.
    /// </summary>
    /// <param name="grid">The rendered parcel grid.</param>
    private static void AssertDraftRowExposesTheAddButton(DataGrid grid)
    {
        var addButtons = FindVisualDescendants<Button>(grid)
            .Where(button => Equals(button.Tag, "DraftAddButton"))
            .ToList();

        Assert.NotEmpty(addButtons);

        var visibleButton = Assert.Single(addButtons, button => button.Visibility == Visibility.Visible);
        var draftRow = Assert.IsType<ParcelRowViewModel>(visibleButton.DataContext);
        Assert.True(draftRow.IsDraft);

        Assert.All(
            addButtons.Where(button => button.DataContext is ParcelRowViewModel { IsDraft: false }),
            button => Assert.NotEqual(Visibility.Visible, button.Visibility));
    }

    /// <summary>Finds every visual descendant of the requested type.</summary>
    private static IEnumerable<TElement> FindVisualDescendants<TElement>(DependencyObject root)
        where TElement : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TElement match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<TElement>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Positions a window far outside the desktop so tests never steal focus.</summary>
    private static TWindow CreateOffScreenWindow<TWindow>(TWindow window)
        where TWindow : Window
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -8000;
        window.Top = -8000;
        window.ShowInTaskbar = false;
        return window;
    }

    /// <summary>Creates the WPF application once per process and loads the merged resource dictionaries.</summary>
    private static void EnsureApplicationLoaded()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var application = new App();

        // Closing a window must not tear the application down, otherwise a later window cannot render.
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        application.InitializeComponent();
    }

    /// <summary>Finds the first logical descendant of the requested type.</summary>
    private static TElement? FindDescendant<TElement>(DependencyObject root)
        where TElement : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is TElement match)
            {
                return match;
            }

            foreach (var child in LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>())
            {
                queue.Enqueue(child);
            }
        }

        return null;
    }

    /// <summary>Runs an action on a dedicated STA thread and rethrows any failure on the caller.</summary>
    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The STA thread did not finish in time.");

        failure?.Throw();
    }
}
