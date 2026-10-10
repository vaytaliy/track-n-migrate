using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailIntegrator.Services;

namespace MailIntegrator.ViewModels;

/// <summary>
/// Drives the "Колонки" popover: the grouped checkbox list, the stored preference and the reset action.
/// </summary>
/// <remarks>
/// The stored set is applied verbatim (locked columns are forced visible, unknown keys are ignored).
/// Every visibility change is persisted immediately, so the choice survives a restart even if the app is
/// closed while the popover is open.
/// </remarks>
public sealed partial class GridColumnLayoutViewModel : ObservableObject
{
    private readonly IColumnLayoutStore _layoutStore;
    private bool _isApplyingState;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridColumnLayoutViewModel"/> class.
    /// </summary>
    /// <param name="layoutStore">The store that holds the operator's chosen columns.</param>
    public GridColumnLayoutViewModel(IColumnLayoutStore layoutStore)
    {
        _layoutStore = layoutStore ?? throw new ArgumentNullException(nameof(layoutStore));

        var storedKeys = layoutStore.LoadVisibleColumnKeys();
        var visibleKeys = new HashSet<string>(
            storedKeys ?? (IReadOnlyCollection<string>)GridColumnCatalog.DefaultVisibleKeys,
            StringComparer.Ordinal);

        GeneralColumns = BuildOptions(GridColumnCatalog.General, visibleKeys);
        SystemColumns = BuildOptions(GridColumnCatalog.System, visibleKeys);
        AllColumns = [.. GeneralColumns, .. SystemColumns];

        foreach (var option in AllColumns)
        {
            option.PropertyChanged += OnOptionPropertyChanged;
        }
    }

    /// <summary>
    /// Gets the business columns, in popover order.
    /// </summary>
    public IReadOnlyList<ColumnOptionViewModel> GeneralColumns { get; }

    /// <summary>
    /// Gets the system columns, in popover order.
    /// </summary>
    public IReadOnlyList<ColumnOptionViewModel> SystemColumns { get; }

    /// <summary>
    /// Gets every toggleable column, business columns first.
    /// </summary>
    public IReadOnlyList<ColumnOptionViewModel> AllColumns { get; }

    /// <summary>
    /// Restores the default visibility of every column and stores the result.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        _isApplyingState = true;
        try
        {
            foreach (var option in AllColumns)
            {
                option.IsVisible = GridColumnCatalog.DefaultVisibleKeys.Contains(option.Key);
            }
        }
        finally
        {
            _isApplyingState = false;
        }

        PersistVisibleColumns();
    }

    /// <summary>
    /// Creates the option view models of one catalog group.
    /// </summary>
    /// <param name="definitions">The columns of the group.</param>
    /// <param name="visibleKeys">The keys that should be visible.</param>
    /// <returns>The option view models, in catalog order.</returns>
    private static List<ColumnOptionViewModel> BuildOptions(
        IReadOnlyList<GridColumnDefinition> definitions,
        IReadOnlySet<string> visibleKeys) =>
        definitions
            .Select(definition => new ColumnOptionViewModel(definition, visibleKeys.Contains(definition.Key)))
            .ToList();

    /// <summary>
    /// Persists the checked columns after the operator toggled one.
    /// </summary>
    /// <param name="sender">The option whose visibility changed.</param>
    /// <param name="e">The change details.</param>
    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingState || e.PropertyName != nameof(ColumnOptionViewModel.IsVisible))
        {
            return;
        }

        PersistVisibleColumns();
    }

    /// <summary>
    /// Writes the currently visible keys to the store.
    /// </summary>
    private void PersistVisibleColumns() =>
        _layoutStore.SaveVisibleColumnKeys(
            AllColumns.Where(option => option.IsVisible).Select(option => option.Key).ToList());
}
