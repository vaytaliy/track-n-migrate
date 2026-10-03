namespace MailIntegrator.Services;

/// <summary>
/// A pluggable carrier integration that adapts a provider specific API to the generic tracking model.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are supplied by the integrator; the application only depends on this contract. Each
/// implementation carries its own <see cref="Descriptor"/> and maps its provider vocabulary onto
/// <see cref="Models.ParcelStatus"/>, falling back to <see cref="Models.ParcelStatus.Unknown"/> when a
/// value cannot be mapped.
/// </para>
/// <para>
/// <see cref="AuthenticateBasicAsync"/> returns an access token that the implementation remembers;
/// <see cref="TrackParcelAsync"/> then uses the current token. The orchestrator authenticates at most
/// once per synchronisation run and per provider.
/// </para>
/// </remarks>
public interface ITrackingService
{
    /// <summary>
    /// Gets the stable code and display name of this provider.
    /// </summary>
    TrackingServiceDescriptor Descriptor { get; }

    /// <summary>
    /// Exchanges basic-auth credentials for an access token and remembers it for later tracking calls.
    /// </summary>
    /// <param name="user">The login configured for this provider.</param>
    /// <param name="password">The password configured for this provider.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The access token issued by the provider.</returns>
    /// <exception cref="Exception">The provider rejected the credentials or the request failed.</exception>
    Task<string> AuthenticateBasicAsync(string user, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Polls the provider for the current status of a parcel.
    /// </summary>
    /// <param name="trackId">The tracking number to poll.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The adapted tracking result.</returns>
    /// <exception cref="Exception">The provider request failed.</exception>
    Task<TrackingResult> TrackParcelAsync(string trackId, CancellationToken cancellationToken);
}
