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
}
