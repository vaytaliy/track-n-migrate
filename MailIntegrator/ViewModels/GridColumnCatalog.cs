namespace MailIntegrator.ViewModels;

/// <summary>
/// The single source of truth for the parcel-grid columns the operator can toggle: the popover order,
/// the grouping, the locked key columns and the default visibility.
/// </summary>
/// <remarks>
/// A column key is the <c>SortMemberPath</c> of the matching XAML column. The always-present
/// <c>Действие</c> column is deliberately not listed here because it can never be hidden.
/// </remarks>
public static class GridColumnCatalog
{
    /// <summary>
    /// Gets the business columns, in the order the popover shows them.
    /// </summary>
    public static IReadOnlyList<GridColumnDefinition> General { get; } =
    [
        new("RowNumber", "#", IsLocked: false, IsVisibleByDefault: true),
        new("PaymentNumber", "Номер счета", IsLocked: true, IsVisibleByDefault: true),
        new("TrackId", "Трекинг #", IsLocked: true, IsVisibleByDefault: true),
        new("TrackingServiceCode", "Служба", IsLocked: false, IsVisibleByDefault: true),
        new("SentDatetimeUtc", "Дата отправки", IsLocked: false, IsVisibleByDefault: false),
        new("ReceivedDatetimeUtc", "Дата получения", IsLocked: false, IsVisibleByDefault: false),
        new("Status", "Посл. статус", IsLocked: false, IsVisibleByDefault: true),
        new("Comment", "Комментарий", IsLocked: false, IsVisibleByDefault: true),
    ];

    /// <summary>
    /// Gets the system columns, in the order the popover shows them.
    /// </summary>
    public static IReadOnlyList<GridColumnDefinition> System { get; } =
    [
        new("CreatedDatetimeUtc", "Дата создания", IsLocked: false, IsVisibleByDefault: true),
        new("LastCheckedDatetimeUtc", "Посл. дата проверки", IsLocked: false, IsVisibleByDefault: false),
        new("IsMigratedTo1CFlag", "1С интеграция", IsLocked: false, IsVisibleByDefault: false),
        new("MigratedTo1CDatetimeUtc", "Дата интеграции в 1С", IsLocked: false, IsVisibleByDefault: false),
    ];

    /// <summary>
    /// Gets every toggleable column, business columns first.
    /// </summary>
    public static IReadOnlyList<GridColumnDefinition> All { get; } = [.. General, .. System];

    /// <summary>
    /// Gets the keys of the columns that are visible before the operator changes anything.
    /// </summary>
    public static IReadOnlySet<string> DefaultVisibleKeys { get; } =
        All.Where(column => column.IsVisibleByDefault)
            .Select(column => column.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Finds a column by its stable key.
    /// </summary>
    /// <param name="key">The column key to look up.</param>
    /// <returns>The matching definition, or <see langword="null"/> when no column has that key.</returns>
    public static GridColumnDefinition? Find(string key) =>
        All.FirstOrDefault(column => string.Equals(column.Key, key, StringComparison.Ordinal));
}
