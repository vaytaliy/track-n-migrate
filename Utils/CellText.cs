namespace MailIntegrator.Utils;

/// <summary>
/// The text and placeholder state of one editable grid cell.
/// </summary>
/// <remarks>
/// Replaces the per-field "display text" property plus its one-line "is the value missing" flag that
/// every editable column used to declare for itself.
/// </remarks>
/// <param name="Text">The text to render: the value itself, or the placeholder when the value is missing.</param>
/// <param name="IsPlaceholder">
/// <see langword="true"/> when <paramref name="Text"/> is a prompt rather than a real value; the view
/// renders such cells in the muted italic placeholder style.
/// </param>
public readonly record struct CellText(string Text, bool IsPlaceholder)
{
    /// <summary>
    /// Resolves a cell value against the placeholder that stands in for a missing value.
    /// </summary>
    /// <param name="value">The raw value, which may be <see langword="null"/> or whitespace.</param>
    /// <param name="placeholder">The text to show when the value is missing.</param>
    /// <returns>The resolved cell text.</returns>
    public static CellText Resolve(string? value, string placeholder) =>
        TextField.IsEmpty(value)
            ? new CellText(placeholder, IsPlaceholder: true)
            : new CellText(value!, IsPlaceholder: false);
}
