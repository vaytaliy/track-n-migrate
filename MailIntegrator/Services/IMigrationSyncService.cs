namespace MailIntegrator.Services;

/// <summary>
/// Runs a migration pass that exports parcels to the 1C target system.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="ITrackingSyncService"/> so the two toolbar actions cannot be confused.
/// The real implementation will use <see cref="ITargetMigrationService"/>; until then a simulated
/// implementation keeps the status strip behaviour intact.
/// </remarks>
public interface IMigrationSyncService
{
    /// <summary>
    /// Performs one migration pass.
    /// </summary>
    /// <param name="cancellationToken">Cancels the running pass.</param>
    /// <returns>A task that completes when the pass finishes.</returns>
    /// <exception cref="Exception">Any failure surfaced by the target system.</exception>
    Task RunAsync(CancellationToken cancellationToken);
}
