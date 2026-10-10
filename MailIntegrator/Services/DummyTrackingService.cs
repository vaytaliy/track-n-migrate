using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// A simulated tracking provider used until real carrier integrations exist.
/// </summary>
/// <remarks>
/// The provider is not a stub: it honours the full <see cref="ITrackingService"/> contract, including the
/// basic-auth handshake and the generic status mapping, so the orchestration and the views can be
/// exercised end to end. Only the network call is absent.
/// </remarks>
public sealed class DummyTrackingService : ITrackingService
{
    private readonly Func<string, TrackingResult> _behavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyTrackingService"/> class.
    /// </summary>
    /// <param name="descriptor">The code and display name of this provider.</param>
    public DummyTrackingService(TrackingServiceDescriptor descriptor)
        : this(descriptor, CreateDefaultResult)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyTrackingService"/> class with a scripted answer.
    /// </summary>
    /// <param name="descriptor">The code and display name of this provider.</param>
    /// <param name="behavior">Produces the result for a tracking number; used by tests to pin a status.</param>
    public DummyTrackingService(TrackingServiceDescriptor descriptor, Func<string, TrackingResult> behavior)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
    }

    /// <inheritdoc />
    public TrackingServiceDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the token issued by the last successful <see cref="AuthenticateBasicAsync"/> call.
    /// </summary>
    public string? AccessToken { get; private set; }

    /// <inheritdoc />
    // TODO: currently simulated - a real provider would call its authentication endpoint and return the
    // token it issued.
    public Task<string> AuthenticateBasicAsync(string user, string password, CancellationToken cancellationToken)
    {
        AccessToken = $"dummy-{Descriptor.Code}-{user}";
        return Task.FromResult(AccessToken);
    }

    /// <inheritdoc />
    // TODO: currently simulated - a real provider would call its tracking endpoint with the access token
    // and map the provider status vocabulary onto ParcelStatus.
    public Task<TrackingResult> TrackParcelAsync(string trackId, CancellationToken cancellationToken) =>
        Task.FromResult(_behavior(trackId));

    /// <summary>
    /// Produces the default simulated answer: the parcel is in transit as of now.
    /// </summary>
    /// <param name="trackId">The tracking number being polled.</param>
    /// <returns>The simulated result.</returns>
    private static TrackingResult CreateDefaultResult(string trackId) =>
        new(trackId, ParcelStatus.InTransit, DateTime.UtcNow);
}
