using System.Windows.Controls;

namespace MailIntegrator.Views.Controls;

/// <summary>
/// Status strip shown in the main window: synchronisation symbol, message, badge and progress line.
/// </summary>
public partial class SyncStatusBar : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStatusBar"/> class.
    /// </summary>
    public SyncStatusBar()
    {
        InitializeComponent();
    }
}
