using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// A pluggable 1C target integration used to check and export parcels.
/// </summary>
/// <remarks>
/// Only the contract is defined in this iteration; no concrete target system is wired up yet.
/// <see cref="AuthenticateBasicAsync"/> returns an access token that the implementation remembers for
/// the subsequent <see cref="CheckIdAsync"/> and <see cref="MigrateOneAsync"/> calls.
/// </remarks>
public interface ITargetMigrationService
{
    /// <summary>
    /// Exchanges basic-auth credentials for an access token and remembers it for later calls.
    /// </summary>
    /// <param name="user">The login configured for the target system.</param>
    /// <param name="password">The password configured for the target system.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The access token issued by the target system.</returns>
    /// <exception cref="Exception">The target system rejected the credentials or the request failed.</exception>
    Task<string> AuthenticateBasicAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a tracking number already exists in the target system.
    /// </summary>
    /// <param name="trackId">The tracking number to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The existing record, or <see langword="null"/> when the number is unknown there.</returns>
    /// <exception cref="Exception">The target system request failed.</exception>
    Task<MigratedParcel?> CheckIdAsync(string trackId, CancellationToken cancellationToken);

    /// <summary>
    /// Exports one parcel to the target system.
    /// </summary>
    /// <param name="trackId">The tracking number to export.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The local parcel record to persist after a successful export.</returns>
    /// <exception cref="Exception">The target system request failed.</exception>
    Task<Parcel> MigrateOneAsync(string trackId, CancellationToken cancellationToken);
}
