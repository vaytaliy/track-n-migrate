using MailIntegrator.Services;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the shared, observable failure list the footer binds to.
/// </summary>
public sealed class UserErrorLogTests
{
    [Fact]
    public void Report_appends_the_error_and_flags_the_log_as_non_empty()
    {
        var log = new UserErrorLog();
        var notifications = new List<string?>();
        log.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        Assert.False(log.HasErrors);

        log.Report(new UserError("Почта России", "RU1", "служба не ответила вовремя"));

        Assert.True(log.HasErrors);
        Assert.Contains(nameof(UserErrorLog.HasErrors), notifications);

        var error = Assert.Single(log.Errors);
        Assert.Equal("Почта России", error.ProviderName);
        Assert.Equal("RU1", error.TrackId);
    }

    [Fact]
    public void Report_only_signals_the_empty_transition_once()
    {
        var log = new UserErrorLog();
        var hasErrorsNotifications = 0;
        log.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UserErrorLog.HasErrors))
            {
                hasErrorsNotifications++;
            }
        };

        log.Report(new UserError("A", null, "первая"));
        log.Report(new UserError("A", null, "вторая"));

        Assert.Equal(1, hasErrorsNotifications);
        Assert.Equal(2, log.Errors.Count);
    }

    [Fact]
    public void BeginPass_empties_the_list_and_flags_it_as_empty()
    {
        var log = new UserErrorLog();
        log.Report(new UserError("A", "RU1", "ошибка"));

        var hasErrorsNotifications = 0;
        log.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UserErrorLog.HasErrors))
            {
                hasErrorsNotifications++;
            }
        };

        log.BeginPass();

        Assert.False(log.HasErrors);
        Assert.Empty(log.Errors);
        Assert.Equal(1, hasErrorsNotifications);
    }

    [Fact]
    public void BeginPass_on_an_empty_log_changes_nothing()
    {
        var log = new UserErrorLog();
        var notifications = 0;
        log.PropertyChanged += (_, _) => notifications++;

        log.BeginPass();

        Assert.False(log.HasErrors);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData("Почта России", null, "нет связи", "Почта России: нет связи")]
    [InlineData("Почта России", "RU1", "нет связи", "Почта России, трек-номер RU1: нет связи")]
    [InlineData("Почта России", "   ", "нет связи", "Почта России: нет связи")]
    public void Display_text_names_the_tracking_number_only_when_there_is_one(
        string providerName,
        string? trackId,
        string reason,
        string expected)
    {
        var error = new UserError(providerName, trackId, reason);

        Assert.Equal(expected, error.DisplayText);
        Assert.Equal(expected, error.ToString());
    }
}
