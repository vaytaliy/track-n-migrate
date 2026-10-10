namespace MailIntegrator.ViewModels;

/// <summary>
/// Describes one toggleable parcel-grid column: its stable key, its caption in the popover, whether the
/// operator may hide it and whether it starts visible.
/// </summary>
/// <param name="Key">
/// The stable identifier of the column. It matches the <c>SortMemberPath</c> of the XAML column, so the
/// catalog, the persistence layer and the view all agree on one name per column.
/// </param>
/// <param name="Header">The text shown next to the checkbox in the columns popover.</param>
/// <param name="IsLocked">
/// <see langword="true"/> for the key columns that must always stay visible; their checkbox is disabled
/// and they are never hidden.
/// </param>
/// <param name="IsVisibleByDefault">
/// <see langword="true"/> when the column is shown before the operator changes anything.
/// </param>
public sealed record GridColumnDefinition(
    string Key,
    string Header,
    bool IsLocked,
    bool IsVisibleByDefault);
