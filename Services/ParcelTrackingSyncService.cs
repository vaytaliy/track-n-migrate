using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="ITrackingSyncService"/> implementation.
/// </summary>
/// <remarks>
/// Parcels are grouped by provider so each provider authenticates once per run, then by tracking number so
/// a duplicated tracking number is polled once and the answer is applied to every row that shares it.
/// Parcels without a provider, and parcels whose provider is no longer registered, are skipped rather than
/// failed, because neither can be routed. Every provider call is routed through a <see cref="ProviderCallGuard"/>:
/// a failure is reported to the shared <see cref="IUserErrorLog"/> and the pass continues with the next
/// tracking number, so one bad parcel can no longer hide the status of all the others. A provider that
/// cannot authenticate is skipped for the rest of the pass, but its failure is reported and the remaining
/// providers still run.
/// </remarks>
public sealed class ParcelTrackingSyncService : ITrackingSyncService
{
    private readonly IParcelService _parcelService;
    private readonly ITrackingServiceRegistry _trackingServiceRegistry;
    private readonly ISecretStore _secretStore;

    /// <summary>
    /// Reports every handled provider failure instead of letting it end the pass.
    /// </summary>
    private readonly ProviderCallGuard _callGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelTrackingSyncService"/> class.
    /// </summary>
    /// <param name="parcelService">Reads and updates the stored parcels.</param>
    /// <param name="trackingServiceRegistry">Resolves the provider that owns a parcel.</param>
    /// <param name="secretStore">Supplies the basic-auth credentials of each provider.</param>
    /// <param name="errorLog">Collects the failures of the current pass for the footer.</param>
    public ParcelTrackingSyncService(
        IParcelService parcelService,
        ITrackingServiceRegistry trackingServiceRegistry,
        ISecretStore secretStore,
        IUserErrorLog errorLog)
    {
        _parcelService = parcelService ?? throw new ArgumentNullException(nameof(parcelService));
        _trackingServiceRegistry =
            trackingServiceRegistry ?? throw new ArgumentNullException(nameof(trackingServiceRegistry));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _callGuard = new ProviderCallGuard(errorLog ?? throw new ArgumentNullException(nameof(errorLog)));
    }

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = _parcelService.GetAllParcels()
            .Where(parcel => !string.IsNullOrWhiteSpace(parcel.TrackingServiceCode))
            .Where(parcel => parcel.LastStatus?.IsFinal() != true)
            .ToList();

        foreach (var providerGroup in pending.GroupBy(
            parcel => parcel.TrackingServiceCode!,
            StringComparer.OrdinalIgnoreCase))
        {
            var service = _trackingServiceRegistry.FindByCode(providerGroup.Key);
            if (service is null)
            {
                // The provider was unregistered while parcels still reference it. This is a data condition
                // rather than a failure of the pass: such parcels cannot be routed at all.
                continue;
            }

            // A provider that cannot authenticate is skipped for this pass only; the other providers run.
            var authenticated = await _callGuard.TryAsync(
                service.Descriptor.DisplayName,
                trackId: null,
                () => AuthenticateAsync(service, cancellationToken));

            if (!authenticated)
            {
                continue;
            }

            // One poll per tracking number: every row that shares it must receive the same answer.
            foreach (var trackGroup in providerGroup.GroupBy(
                parcel => parcel.TrackId,
                StringComparer.OrdinalIgnoreCase))
            {
                await _callGuard.TryAsync(
                    service.Descriptor.DisplayName,
                    trackGroup.Key,
                    () => PollAndApplyAsync(service, providerGroup.Key, trackGroup.Key, cancellationToken));
            }
        }
    }

    /// <inheritdoc />
    public async Task<TrackingResult?> RunForParcelAsync(long parcelId, CancellationToken cancellationToken)
    {
        var parcel = _parcelService.GetParcel(parcelId);
        if (parcel is null
            || string.IsNullOrWhiteSpace(parcel.TrackingServiceCode)
            || parcel.LastStatus?.IsFinal() == true)
        {
            return null;
        }

        var service = _trackingServiceRegistry.FindByCode(parcel.TrackingServiceCode);
        if (service is null)
        {
            // The operator asked for this row explicitly, so the unroutable state is reported here.
            _callGuard.Report(
                parcel.TrackingServiceCode!,
                parcel.TrackId,
                new SyncException("служба не зарегистрирована"));
            return null;
        }

        // Exactly like the bulk pass, the handshake runs before any polling; a failed handshake is reported
        // and nothing is polled for this parcel.
        var authenticated = await _callGuard.TryAsync(
            service.Descriptor.DisplayName,
            trackId: null,
            () => AuthenticateAsync(service, cancellationToken));

        if (!authenticated)
        {
            return null;
        }

        return await _callGuard.TryAsync<TrackingResult?>(
            service.Descriptor.DisplayName,
            parcel.TrackId,
            () => PollAndApplyAsync(
                service,
                parcel.TrackingServiceCode!,
                parcel.TrackId,
                cancellationToken));
    }

    /// <summary>
    /// Polls one tracking number with an already authenticated provider and applies the reported status to
    /// every parcel registered under it.
    /// </summary>
    /// <param name="service">The authenticated provider that owns the tracking number.</param>
    /// <param name="trackingServiceCode">The provider code the tracking number was polled under.</param>
    /// <param name="trackId">The tracking number to poll.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The applied result, or <see langword="null"/> when every parcel under that tracking number was
    /// already in a final state.
    /// </returns>
    private async Task<TrackingResult?> PollAndApplyAsync(
        ITrackingService service,
        string trackingServiceCode,
        string trackId,
        CancellationToken cancellationToken)
    {
        var result = await service.TrackParcelAsync(trackId, cancellationToken);
        var updated = _parcelService.ApplyTrackingResultToTrackId(trackingServiceCode, trackId, result);
        return updated > 0 ? result : null;
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
            throw new SyncException("не заданы учётные данные");
        }

        await service.AuthenticateBasicAsync(credential.Login, credential.Password, cancellationToken);
    }
}
