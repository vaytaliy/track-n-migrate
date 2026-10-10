using MailIntegrator.Services;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Synchronisation stub that completes immediately, optionally failing on demand or running a scripted pass.
/// </summary>
/// <remarks>
/// Implements both passes so a single instance can be handed to the tracking and migration commands; the
/// tests that care about a specific pass inspect <see cref="RequestedPass"/>.
/// </remarks>
public sealed class FakeSyncService : ITrackingSyncService, IMigrationSyncService
{
    /// <summary>
    /// Identifies which pass was requested.
    /// </summary>
    public enum SyncPass
    {
        /// <summary>The tracking pass behind "Запросить статус посылок".</summary>
        Tracking,

        /// <summary>The migration pass behind "Экспорт в 1С".</summary>
        Migration,
    }

    /// <summary>
    /// Gets the number of times a synchronisation pass was requested.
    /// </summary>
    public int RunCount { get; private set; }

    /// <summary>
    /// Gets the passes that were requested, in order.
    /// </summary>
    public List<SyncPass> RequestedPass { get; } = [];

    /// <summary>
    /// Gets or sets the exception to throw for the next pass.
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// Gets or sets a scripted tracking pass that can mutate parcel data.
    /// </summary>
    public Func<CancellationToken, Task>? TrackingPass { get; set; }

    /// <summary>
    /// Gets or sets the scripted answer of a single parcel check; when absent, no status is applied.
    /// </summary>
    public Func<long, TrackingResult?>? SingleParcelCheck { get; set; }

    /// <summary>
    /// Gets the identifiers of the parcels whose status was checked individually, in order.
    /// </summary>
    public List<long> CheckedParcelIds { get; } = [];

    /// <inheritdoc />
    Task ITrackingSyncService.RunAsync(CancellationToken cancellationToken) =>
        RunAsync(SyncPass.Tracking, cancellationToken);

    /// <inheritdoc />
    Task IMigrationSyncService.RunAsync(CancellationToken cancellationToken) =>
        RunAsync(SyncPass.Migration, cancellationToken);

    /// <inheritdoc />
    Task<TrackingResult?> ITrackingSyncService.RunForParcelAsync(
        long parcelId,
        CancellationToken cancellationToken)
    {
        CheckedParcelIds.Add(parcelId);

        if (ExceptionToThrow is not null)
        {
            return Task.FromException<TrackingResult?>(ExceptionToThrow);
        }

        return Task.FromResult(SingleParcelCheck?.Invoke(parcelId));
    }

    /// <summary>
    /// Records the pass and produces the configured outcome.
    /// </summary>
    /// <param name="pass">The pass that was requested.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>The configured task.</returns>
    private Task RunAsync(SyncPass pass, CancellationToken cancellationToken)
    {
        RunCount++;
        RequestedPass.Add(pass);

        if (ExceptionToThrow is not null)
        {
            return Task.FromException(ExceptionToThrow);
        }

        return pass == SyncPass.Tracking && TrackingPass is not null
            ? TrackingPass(cancellationToken)
            : Task.CompletedTask;
    }
}
