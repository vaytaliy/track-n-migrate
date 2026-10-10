using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Views;

/// <summary>
/// Applies the operator's column choice to the parcel grid and keeps the two in step.
/// </summary>
/// <remarks>
/// This is view-only plumbing: the catalog and the persisted preference live in the view models, while
/// this class only translates a key into the matching <see cref="DataGridColumn"/> (matched by
/// <see cref="DataGridColumn.SortMemberPath"/>) and toggles its visibility. Columns without a catalog
/// entry, such as the always-present action column, are never touched.
/// </remarks>
public sealed class GridColumnVisibilityBinder
{
    private readonly DataGrid _grid;
    private readonly GridColumnLayoutViewModel _layout;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridColumnVisibilityBinder"/> class and applies the
    /// current visibility immediately.
    /// </summary>
    /// <param name="grid">The grid whose columns are managed.</param>
    /// <param name="layout">The layout view model that holds the visibility state.</param>
    public GridColumnVisibilityBinder(DataGrid grid, GridColumnLayoutViewModel layout)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        foreach (var option in _layout.AllColumns)
        {
            Apply(option);
            option.PropertyChanged += OnOptionPropertyChanged;
        }
    }

    /// <summary>
    /// Applies a single option to its column.
    /// </summary>
    /// <param name="option">The option whose visibility changed.</param>
    private void Apply(ColumnOptionViewModel option)
    {
        var column = FindColumn(option.Key);
        if (column is null)
        {
            return;
        }

        column.Visibility = option.IsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Reacts to a checkbox toggle in the popover.
    /// </summary>
    /// <param name="sender">The option whose visibility changed.</param>
    /// <param name="e">The change details.</param>
    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnOptionViewModel.IsVisible)
            && sender is ColumnOptionViewModel option)
        {
            Apply(option);
        }
    }

    /// <summary>
    /// Finds the grid column that carries a catalog key.
    /// </summary>
    /// <param name="columnKey">The catalog key, which equals the column's sort member path.</param>
    /// <returns>The matching column, or <see langword="null"/> when the grid has no such column.</returns>
    private DataGridColumn? FindColumn(string columnKey) =>
        _grid.Columns.FirstOrDefault(column => string.Equals(
            column.SortMemberPath,
            columnKey,
            StringComparison.Ordinal));
}
