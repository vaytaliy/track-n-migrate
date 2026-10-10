using MailIntegrator.Models;
using MailIntegrator.Services;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Scriptable tracking provider that records how the orchestration used it.
/// </summary>
public sealed class FakeTrackingService : ITrackingService
{
    private readonly Func<string, TrackingResult> _behavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTrackingService"/> class.
    /// </summary>
    /// <param name="code">The provider code.</param>
    /// <param name="displayName">The provider display name.</param>
    /// <param name="behavior">Produces the result for a tracking number; defaults to "in transit".</param>
    public FakeTrackingService(
        string code,
        string displayName,
        Func<string, TrackingResult>? behavior = null)
    {
        Descriptor = new TrackingServiceDescriptor(code, displayName);
        _behavior = behavior ?? (trackId => new TrackingResult(trackId, ParcelStatus.InTransit, DefaultStatusInstant));
    }

    /// <summary>
    /// Gets the fixed instant used by the default behavior.
    /// </summary>
    public static DateTime DefaultStatusInstant { get; } = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public TrackingServiceDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the number of authentication handshakes performed.
    /// </summary>
    public int AuthenticateCount { get; private set; }

    /// <summary>
    /// Gets the login supplied to the last authentication handshake.
    /// </summary>
    public string? LastUser { get; private set; }

    /// <summary>
    /// Gets the tracking numbers that were polled, in order.
    /// </summary>
    public List<string> RequestedTrackIds { get; } = [];

    /// <summary>
    /// Gets the failures to raise instead of a result, keyed by tracking number (case-insensitive).
    /// </summary>
    public Dictionary<string, Exception> Failures { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<string> AuthenticateBasicAsync(string user, string password, CancellationToken cancellationToken)
    {
        AuthenticateCount++;
        LastUser = user;
        return Task.FromResult($"token-{Descriptor.Code}");
    }

    /// <inheritdoc />
    public Task<TrackingResult> TrackParcelAsync(string trackId, CancellationToken cancellationToken)
    {
        RequestedTrackIds.Add(trackId);

        return Failures.TryGetValue(trackId, out var failure)
            ? Task.FromException<TrackingResult>(failure)
            : Task.FromResult(_behavior(trackId));
    }
}
