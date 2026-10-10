using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using MailIntegrator.Services;

namespace MailIntegrator.Tests.Unit.Algorithms;

/// <summary>
/// Covers the single mapping from a failed provider call to the short Russian reason shown in the footer.
/// </summary>
public sealed class TrackingFailureDescriberTests
{
    [Fact]
    public void Sync_exception_supplies_its_own_reason() =>
        Assert.Equal(
            "не вернула историю операций",
            TrackingFailureDescriber.Describe(new SyncException("не вернула историю операций")));

    [Fact]
    public void Unauthorized_access_reads_as_rejected_credentials() =>
        Assert.Equal(
            TrackingFailureDescriber.RejectedCredentials,
            TrackingFailureDescriber.Describe(new UnauthorizedAccessException("пароль не подошёл")));

    [Fact]
    public void Http_failure_with_a_status_code_names_the_code() =>
        Assert.Equal(
            "служба ответила ошибкой 503",
            TrackingFailureDescriber.Describe(
                new HttpRequestException("boom", inner: null, HttpStatusCode.ServiceUnavailable)));

    [Fact]
    public void Http_failure_without_a_status_code_reads_as_unreachable() =>
        Assert.Equal(
            TrackingFailureDescriber.UnreachableService,
            TrackingFailureDescriber.Describe(new HttpRequestException("boom")));

    [Fact]
    public void Socket_failure_reads_as_unreachable() =>
        Assert.Equal(
            TrackingFailureDescriber.UnreachableService,
            TrackingFailureDescriber.Describe(new SocketException(10061)));

    [Fact]
    public void An_uncancelled_cancellation_reads_as_a_timeout() =>
        Assert.Equal(
            TrackingFailureDescriber.TimedOut,
            TrackingFailureDescriber.Describe(new TaskCanceledException()));

    [Fact]
    public void An_unknown_failure_reads_as_unexpected() =>
        Assert.Equal(
            TrackingFailureDescriber.UnexpectedFailure,
            TrackingFailureDescriber.Describe(new InvalidOperationException("HTTP 503")));

    [Fact]
    public void The_message_of_an_untrusted_exception_is_never_echoed()
    {
        var reason = TrackingFailureDescriber.Describe(new InvalidOperationException("password=hunter2"));

        Assert.DoesNotContain("hunter2", reason, StringComparison.Ordinal);
    }
}
