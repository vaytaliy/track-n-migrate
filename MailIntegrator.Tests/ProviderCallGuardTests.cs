using System.Net.Http;
using MailIntegrator.Services;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the reuse seam of the error handling: a failed provider call becomes one reported error and never
/// an exception, while a real cancellation still propagates.
/// </summary>
public sealed class ProviderCallGuardTests
{
    [Fact]
    public async Task TryAsync_returns_the_value_of_a_successful_call()
    {
        var guard = new ProviderCallGuard(new UserErrorLog());

        var result = await guard.TryAsync("Почта России", "RU1", () => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task TryAsync_returns_true_for_a_successful_void_call()
    {
        var guard = new ProviderCallGuard(new UserErrorLog());

        Assert.True(await guard.TryAsync("DHL", "RU1", () => Task.CompletedTask));
    }

    [Fact]
    public async Task TryAsync_reports_a_value_failure_and_returns_the_default()
    {
        var log = new UserErrorLog();
        var guard = new ProviderCallGuard(log);

        var result = await guard.TryAsync<int>(
            "Почта России",
            "RU1",
            () => Task.FromException<int>(new HttpRequestException("boom")));

        Assert.Equal(0, result);

        var error = Assert.Single(log.Errors);
        Assert.Equal("Почта России", error.ProviderName);
        Assert.Equal("RU1", error.TrackId);
        Assert.Equal(TrackingFailureDescriber.UnreachableService, error.Reason);
    }

    [Fact]
    public async Task TryAsync_reports_a_void_failure_and_returns_false()
    {
        var log = new UserErrorLog();
        var guard = new ProviderCallGuard(log);

        var succeeded = await guard.TryAsync(
            "Почта России",
            trackId: null,
            () => Task.FromException(new SyncException("не заданы учётные данные")));

        Assert.False(succeeded);

        var error = Assert.Single(log.Errors);
        Assert.Null(error.TrackId);
        Assert.Equal("не заданы учётные данные", error.Reason);
    }

    [Fact]
    public async Task TryAsync_rethrows_a_real_cancellation_without_reporting_it()
    {
        var log = new UserErrorLog();
        var guard = new ProviderCallGuard(log);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => guard.TryAsync("DHL", "RU1", () => Task.FromCanceled<int>(cancellation.Token)));

        Assert.False(log.HasErrors);
    }

    [Fact]
    public async Task TryAsync_reports_an_uncancelled_cancellation_as_a_timeout()
    {
        var log = new UserErrorLog();
        var guard = new ProviderCallGuard(log);

        await guard.TryAsync<int>("DHL", "RU1", () => Task.FromException<int>(new TaskCanceledException()));

        Assert.Equal(TrackingFailureDescriber.TimedOut, Assert.Single(log.Errors).Reason);
    }

    [Fact]
    public void Report_explains_a_failure_the_caller_handled_itself()
    {
        var log = new UserErrorLog();
        var guard = new ProviderCallGuard(log);

        guard.Report("RemovedCarrier", "RU1", new SyncException("служба не зарегистрирована"));

        Assert.Equal(
            "RemovedCarrier, трек-номер RU1: служба не зарегистрирована",
            Assert.Single(log.Errors).DisplayText);
    }
}
