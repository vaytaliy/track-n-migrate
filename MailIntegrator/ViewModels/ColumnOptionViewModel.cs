using CommunityToolkit.Mvvm.ComponentModel;

namespace MailIntegrator.ViewModels;

/// <summary>
/// One row of the columns popover: the column it represents and whether that column is currently shown.
/// </summary>
/// <remarks>
/// The <see cref="IsVisible"/> change is what the layout view model persists and what the view's
/// visibility binder reacts to, so both consumers subscribe to this single notification.
/// </remarks>
public sealed partial class ColumnOptionViewModel : ObservableObject
{
    /// <summary>
    /// Backing field for <see cref="IsVisible"/>.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnOptionViewModel"/> class.
    /// </summary>
    /// <param name="definition">The column descriptor from the catalog.</param>
    /// <param name="isVisible">Whether the column is currently shown.</param>
    public ColumnOptionViewModel(GridColumnDefinition definition, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Key = definition.Key;
        Header = definition.Header;
        IsLocked = definition.IsLocked;

        // A locked key column can never be hidden, even by a corrupted stored value.
        _isVisible = isVisible || definition.IsLocked;
    }

    /// <summary>
    /// Gets the stable column key, which equals the matching grid column's <c>SortMemberPath</c>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the caption shown next to the checkbox.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Gets a value indicating whether the column is a key column that must always stay visible.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Gets a value indicating whether the operator may change <see cref="IsVisible"/>.
    /// </summary>
    public bool CanToggle => !IsLocked;
}
