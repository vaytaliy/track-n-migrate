using CommunityToolkit.Mvvm.ComponentModel;
using MailIntegrator.Models;

namespace MailIntegrator.ViewModels;

/// <summary>
/// Exposes the synchronisation state consumed by the status strip.
/// </summary>
/// <remarks>
/// Only presentation concerns live here; the state transitions are driven by the owning view model.
/// </remarks>
public sealed partial class SyncStatusViewModel : ObservableObject
{
    /// <summary>
    /// The message shown while a synchronisation pass is running.
    /// </summary>
    public const string InProgressMessage = "Синхронизация в процессе..";

    /// <summary>
    /// The message shown when a synchronisation pass fails.
    /// </summary>
    public const string FailedMessage = "Ошибка синхронизации";

    /// <summary>
    /// The message shown when a synchronisation pass succeeds.
    /// </summary>
    public const string SucceededMessage = "Синхронизация успешна";

    /// <summary>
    /// The badge caption shown while a synchronisation pass is running.
    /// </summary>
    public const string InProgressBadge = "В ПРОЦЕССЕ";

    /// <summary>
    /// The badge caption shown when a synchronisation pass fails.
    /// </summary>
    public const string FailedBadge = "ОШИБКА";

    /// <summary>
    /// The badge caption shown when a synchronisation pass succeeds.
    /// </summary>
    public const string SucceededBadge = "УСПЕШНО";

    /// <summary>
    /// Backing field for <see cref="State"/>.
    /// </summary>
    [ObservableProperty]
    private SyncState _state = SyncState.Idle;

    /// <summary>
    /// Backing field for <see cref="LastErrorMessage"/>.
    /// </summary>
    [ObservableProperty]
    private string? _lastErrorMessage;

    /// <summary>
    /// Gets a value indicating whether the status strip should be visible at all.
    /// </summary>
    public bool HasMessage => State != SyncState.Idle;

    /// <summary>
    /// Gets a value indicating whether a synchronisation pass is running.
    /// </summary>
    public bool IsInProgress => State == SyncState.InProgress;

    /// <summary>
    /// Gets a value indicating whether the indeterminate progress bar should be shown.
    /// </summary>
    public bool IsProgressVisible => IsInProgress;

    /// <summary>
    /// Gets a value indicating whether the animated spinner should be shown.
    /// </summary>
    public bool IsSpinnerVisible => IsInProgress;

    /// <summary>
    /// Gets a value indicating whether the warning symbol should be shown.
    /// </summary>
    public bool IsWarningVisible => State == SyncState.Failed;

    /// <summary>
    /// Gets a value indicating whether the success symbol should be shown.
    /// </summary>
    public bool IsSuccessVisible => State == SyncState.Succeeded;

    /// <summary>
    /// Gets the status message for the current state.
    /// </summary>
    public string Message => State switch
    {
        SyncState.InProgress => InProgressMessage,
        SyncState.Failed => FailedMessage,
        SyncState.Succeeded => SucceededMessage,
        _ => string.Empty,
    };

    /// <summary>
    /// Gets the badge caption for the current state.
    /// </summary>
    public string BadgeText => State switch
    {
        SyncState.InProgress => InProgressBadge,
        SyncState.Failed => FailedBadge,
        SyncState.Succeeded => SucceededBadge,
        _ => string.Empty,
    };

    /// <summary>
    /// Gets the tooltip shown over the status symbol, carrying any handled exception message.
    /// </summary>
    public string SymbolTooltip => LastErrorMessage ?? Message;

    /// <summary>
    /// Notifies every computed member that depends on the synchronisation state.
    /// </summary>
    /// <param name="value">The new state.</param>
    partial void OnStateChanged(SyncState value) => NotifyComputedMembers();

    /// <summary>
    /// Notifies the tooltip when a new error message arrives.
    /// </summary>
    /// <param name="value">The new error message.</param>
    partial void OnLastErrorMessageChanged(string? value) => OnPropertyChanged(nameof(SymbolTooltip));

    /// <summary>
    /// Raises change notifications for all members derived from <see cref="State"/>.
    /// </summary>
    private void NotifyComputedMembers()
    {
        OnPropertyChanged(nameof(HasMessage));
        OnPropertyChanged(nameof(IsInProgress));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(IsSpinnerVisible));
        OnPropertyChanged(nameof(IsWarningVisible));
        OnPropertyChanged(nameof(IsSuccessVisible));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(SymbolTooltip));
    }
}
