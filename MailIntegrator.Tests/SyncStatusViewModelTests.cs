using MailIntegrator.Models;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the status strip state mapping.
/// </summary>
public sealed class SyncStatusViewModelTests
{
    [Fact]
    public void Idle_state_shows_nothing()
    {
        var status = new SyncStatusViewModel();

        Assert.Equal(SyncState.Idle, status.State);
        Assert.False(status.HasMessage);
        Assert.Equal(string.Empty, status.Message);
        Assert.Equal(string.Empty, status.BadgeText);
        Assert.False(status.IsInProgress);
        Assert.False(status.IsProgressVisible);
        Assert.False(status.IsSpinnerVisible);
        Assert.False(status.IsWarningVisible);
        Assert.False(status.IsSuccessVisible);
    }

    [Fact]
    public void InProgress_state_shows_the_spinner_and_the_progress_line()
    {
        var status = new SyncStatusViewModel { State = SyncState.InProgress };

        Assert.True(status.HasMessage);
        Assert.Equal(SyncStatusViewModel.InProgressMessage, status.Message);
        Assert.Equal(SyncStatusViewModel.InProgressBadge, status.BadgeText);
        Assert.True(status.IsInProgress);
        Assert.True(status.IsProgressVisible);
        Assert.True(status.IsSpinnerVisible);
        Assert.False(status.IsWarningVisible);
        Assert.False(status.IsSuccessVisible);
    }

    [Fact]
    public void Succeeded_state_shows_the_checkmark_and_no_progress()
    {
        var status = new SyncStatusViewModel { State = SyncState.Succeeded };

        Assert.Equal(SyncStatusViewModel.SucceededMessage, status.Message);
        Assert.Equal(SyncStatusViewModel.SucceededBadge, status.BadgeText);
        Assert.True(status.IsSuccessVisible);
        Assert.False(status.IsProgressVisible);
        Assert.False(status.IsSpinnerVisible);
        Assert.False(status.IsWarningVisible);
    }

    [Fact]
    public void Failed_state_shows_the_warning_symbol()
    {
        var status = new SyncStatusViewModel { State = SyncState.Failed };

        Assert.Equal(SyncStatusViewModel.FailedMessage, status.Message);
        Assert.Equal(SyncStatusViewModel.FailedBadge, status.BadgeText);
        Assert.True(status.IsWarningVisible);
        Assert.False(status.IsProgressVisible);
        Assert.False(status.IsSuccessVisible);
    }

    [Fact]
    public void SymbolTooltip_falls_back_to_the_message_and_prefers_the_error_details()
    {
        var status = new SyncStatusViewModel { State = SyncState.Failed };
        Assert.Equal(SyncStatusViewModel.FailedMessage, status.SymbolTooltip);

        status.LastErrorMessage = "HTTP 503";
        Assert.Equal("HTTP 503", status.SymbolTooltip);
    }

    [Fact]
    public void Changing_the_state_notifies_every_computed_member()
    {
        var status = new SyncStatusViewModel();
        var raised = new List<string?>();
        status.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        status.State = SyncState.Succeeded;

        Assert.Contains(nameof(SyncStatusViewModel.HasMessage), raised);
        Assert.Contains(nameof(SyncStatusViewModel.Message), raised);
        Assert.Contains(nameof(SyncStatusViewModel.BadgeText), raised);
        Assert.Contains(nameof(SyncStatusViewModel.IsProgressVisible), raised);
        Assert.Contains(nameof(SyncStatusViewModel.IsSpinnerVisible), raised);
        Assert.Contains(nameof(SyncStatusViewModel.IsWarningVisible), raised);
        Assert.Contains(nameof(SyncStatusViewModel.IsSuccessVisible), raised);
        Assert.Contains(nameof(SyncStatusViewModel.IsInProgress), raised);
    }
}
