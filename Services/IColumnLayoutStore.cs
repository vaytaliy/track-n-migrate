namespace MailIntegrator.Services;

/// <summary>
/// Persists which parcel-grid columns the operator chose to see.
/// </summary>
/// <remarks>
/// The store deals only in column keys; it does not know the catalog, so it can be exercised without the
/// view models. Implementations must tolerate unknown keys, because a future version may remove a column.
/// </remarks>
public interface IColumnLayoutStore
{
    /// <summary>
    /// Reads the stored visible-column keys.
    /// </summary>
    /// <returns>
    /// The stored keys, or <see langword="null"/> when the operator never chose and the caller should fall
    /// back to the catalog defaults.
    /// </returns>
    IReadOnlyList<string>? LoadVisibleColumnKeys();

    /// <summary>
    /// Stores the visible-column keys, replacing any previous set.
    /// </summary>
    /// <param name="keys">The keys that should be visible.</param>
    void SaveVisibleColumnKeys(IReadOnlyCollection<string> keys);
}
