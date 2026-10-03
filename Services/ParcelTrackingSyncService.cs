using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="ITrackingSyncService"/> implementation.
/// </summary>
/// <remarks>
/// Parcels are grouped by provider so each provider authenticates once per run. Parcels without a
/// provider, and parcels whose provider is no longer registered, are skipped rather than failed, because
/// neither can be routed. A configured provider that is missing its credentials fails the whole pass:
/// silently skipping its parcels would leave the operator with stale data and no explanation.
/// </remarks>
public sealed class ParcelTrackingSyncService : ITrackingSyncService
{
    private readonly IParcelService _parcelService;
    private readonly ITrackingServiceRegistry _trackingServiceRegistry;
    private readonly ISecretStore _secretStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelTrackingSyncService"/> class.
    /// </summary>
    /// <param name="parcelService">Reads and updates the stored parcels.</param>
    /// <param name="trackingServiceRegistry">Resolves the provider that owns a parcel.</param>
    /// <param name="secretStore">Supplies the basic-auth credentials of each provider.</param>
    public ParcelTrackingSyncService(
        IParcelService parcelService,
        ITrackingServiceRegistry trackingServiceRegistry,
        ISecretStore secretStore)
    {
        _parcelService = parcelService ?? throw new ArgumentNullException(nameof(parcelService));
        _trackingServiceRegistry =
            trackingServiceRegistry ?? throw new ArgumentNullException(nameof(trackingServiceRegistry));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = _parcelService.GetAllParcels()
            .Where(parcel => !string.IsNullOrWhiteSpace(parcel.TrackingServiceCode))
            .Where(parcel => parcel.LastStatus?.IsFinal() != true)
            .ToList();

        foreach (var group in pending.GroupBy(
            parcel => parcel.TrackingServiceCode!,
            StringComparer.OrdinalIgnoreCase))
        {
            var service = _trackingServiceRegistry.FindByCode(group.Key);
            if (service is null)
            {
                // The provider was unregistered while parcels still reference it.
                continue;
            }

            await AuthenticateAsync(service, cancellationToken);

            foreach (var parcel in group)
            {
                var result = await service.TrackParcelAsync(parcel.TrackId, cancellationToken);
                _parcelService.ApplyTrackingResult(parcel.Id, result);
            }
        }
    }

    /// <summary>
    /// Reads the stored credential of a provider and performs the basic-auth handshake.
    /// </summary>
    /// <param name="service">The provider about to be polled.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="SyncException">No credential is configured for the provider.</exception>
    private async Task AuthenticateAsync(ITrackingService service, CancellationToken cancellationToken)
    {
        var credential = _secretStore.GetCredential(service.Descriptor.ToCredentialTarget());
        if (credential is null)
        {
            throw new SyncException($"Не заданы учётные данные для службы «{service.Descriptor.DisplayName}».");
        }

        await service.AuthenticateBasicAsync(credential.Login, credential.Password, cancellationToken);
    }
}
