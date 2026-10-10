namespace MailIntegrator.Services;

/// <summary>
/// Runs a tracking synchronisation pass against the registered carriers.
/// </summary>
public interface ITrackingSyncService
{
    /// <summary>
    /// Polls every parcel that is not in a final state and applies the reported status.
    /// </summary>
    /// <param name="cancellationToken">Cancels the running pass.</param>
    /// <returns>A task that completes when the pass finishes.</returns>
    /// <exception cref="SyncException">A provider is not configured, or a provider request failed.</exception>
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Polls a single parcel and applies the reported status, so the operator can refresh one row without
    /// running the whole pass.
    /// </summary>
    /// <param name="parcelId">The identifier of the parcel to poll.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The applied result, or <see langword="null"/> when nothing was applied because the parcel does not
    /// exist, has no provider, or already reached a final status.
    /// </returns>
    /// <exception cref="SyncException">The provider is not configured, or the provider request failed.</exception>
    Task<TrackingResult?> RunForParcelAsync(long parcelId, CancellationToken cancellationToken);
}
