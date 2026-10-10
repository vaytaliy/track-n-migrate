using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MailIntegrator.Views.Controls;

/// <summary>
/// Fixed-height footer that lists handled failures of the last action in a subdued red, one line per message.
/// </summary>
/// <remarks>
/// The control owns no error logic: it renders whatever <see cref="Messages"/> it is given, so the same
/// component can show the failures of any screen. The main window binds it to the shared user error log.
/// </remarks>
public partial class StatusFooter : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Messages"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MessagesProperty = DependencyProperty.Register(
        nameof(Messages),
        typeof(IEnumerable),
        typeof(StatusFooter),
        new PropertyMetadata(default(IEnumerable)));

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusFooter"/> class.
    /// </summary>
    public StatusFooter()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the messages to display, in order. Each item is rendered through its
    /// <see cref="object.ToString"/> result, so plain strings are accepted as well.
    /// </summary>
    public IEnumerable? Messages
    {
        get => (IEnumerable?)GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }
}
