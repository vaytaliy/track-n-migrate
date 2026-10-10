using System.Windows.Controls;

namespace MailIntegrator.Views.Controls;

/// <summary>
/// Column selector popover. Purely presentational: the options, the persistence and the reset action all
/// live in <see cref="ViewModels.GridColumnLayoutViewModel"/>.
/// </summary>
public partial class ColumnsPopover : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnsPopover"/> class.
    /// </summary>
    public ColumnsPopover() => InitializeComponent();
}
