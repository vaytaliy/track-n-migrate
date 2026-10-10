using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MailIntegrator.Utils;

namespace MailIntegrator.Views.Controls;

/// <summary>
/// Shared display cell for the editable string columns of the parcel grid.
/// </summary>
/// <remarks>
/// The editable columns (payment number, tracking number, comment) used to repeat the same
/// icon/placeholder/pencil markup. They now differ only through the dependency properties below, so the
/// markup lives here once. The DataGrid still owns the editing half: the column's editing template
/// supplies the <see cref="TextBox"/> so the row-level validation errors keep flowing straight into it. The
/// edit pencil is declared once in the column header instead of on every cell.
/// </remarks>
public partial class EditableTextCell : UserControl
{
    /// <summary>
    /// Identifies the <see cref="CellContent"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CellContentProperty = DependencyProperty.Register(
        nameof(CellContent),
        typeof(CellText),
        typeof(EditableTextCell),
        new PropertyMetadata(default(CellText)));

    /// <summary>
    /// Identifies the <see cref="IsEmphasised"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsEmphasisedProperty = DependencyProperty.Register(
        nameof(IsEmphasised),
        typeof(bool),
        typeof(EditableTextCell),
        new PropertyMetadata(false));

    /// <summary>
    /// Identifies the <see cref="LinkUrl"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LinkUrlProperty = DependencyProperty.Register(
        nameof(LinkUrl),
        typeof(Uri),
        typeof(EditableTextCell),
        new PropertyMetadata(default(Uri)));

    /// <summary>
    /// Identifies the <see cref="IsEditable"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable),
        typeof(bool),
        typeof(EditableTextCell),
        new PropertyMetadata(false));

    /// <summary>
    /// Identifies the <see cref="IsLocked"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
        nameof(IsLocked),
        typeof(bool),
        typeof(EditableTextCell),
        new PropertyMetadata(false));

    /// <summary>
    /// Identifies the <see cref="Caption"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption),
        typeof(string),
        typeof(EditableTextCell),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies the <see cref="TextWrapping"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
        nameof(TextWrapping),
        typeof(TextWrapping),
        typeof(EditableTextCell),
        new PropertyMetadata(TextWrapping.NoWrap));

    /// <summary>
    /// Initializes a new instance of the <see cref="EditableTextCell"/> class.
    /// </summary>
    public EditableTextCell()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the resolved text and placeholder state of the cell.
    /// </summary>
    public CellText CellContent
    {
        get => (CellText)GetValue(CellContentProperty);
        set => SetValue(CellContentProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the cell emphasises its value as an identifier: an editable
    /// value is then drawn in the accent colour and semi-bold. It no longer controls any icon; the edit
    /// hint lives in the column header and the optional tracking link is driven by <see cref="LinkUrl"/>.
    /// </summary>
    public bool IsEmphasised
    {
        get => (bool)GetValue(IsEmphasisedProperty);
        set => SetValue(IsEmphasisedProperty, value);
    }

    /// <summary>
    /// Gets or sets the public tracking page of the value, or <see langword="null"/> when there is nothing
    /// to open. The link icon is rendered only while a URL is present.
    /// </summary>
    public Uri? LinkUrl
    {
        get => (Uri?)GetValue(LinkUrlProperty);
        set => SetValue(LinkUrlProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the value may still be edited on this row.
    /// </summary>
    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the value is locked and therefore rendered muted and italic.
    /// </summary>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    /// <summary>
    /// Gets or sets the optional caption rendered under the value, for example the comment lock notice.
    /// An empty caption is not rendered.
    /// </summary>
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>
    /// Gets or sets how the value wraps, so the comment column can wrap while the identifier columns do not.
    /// </summary>
    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty);
        set => SetValue(TextWrappingProperty, value);
    }

    /// <summary>
    /// Opens the tracking page of the value in the operator's default browser.
    /// </summary>
    /// <param name="sender">The link button.</param>
    /// <param name="e">The event arguments.</param>
    /// <remarks>
    /// A browser that cannot be started must not tear the window down, so a failed launch is swallowed:
    /// the operator can still copy the tracking number manually.
    /// </remarks>
    private void OnTrackingLinkClick(object sender, RoutedEventArgs e)
    {
        if (LinkUrl is not { } url)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No default browser (or a rejected URL): nothing to do from the cell.
        }
    }
}
