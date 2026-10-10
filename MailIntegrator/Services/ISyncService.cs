namespace MailIntegrator.Services;

/// <summary>
/// Runs a synchronisation pass against the external services.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Performs one synchronisation pass.
    /// </summary>
    /// <param name="cancellationToken">Cancels the running pass.</param>
    /// <returns>A task that completes when the pass finishes.</returns>
    /// <exception cref="Exception">Any failure surfaced by the external services.</exception>
    Task RunAsync(CancellationToken cancellationToken);
}
