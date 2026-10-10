using MailIntegrator.Configuration;

namespace MailIntegrator.Services;

/// <summary>
/// Builds the tracking providers that ship with the application.
/// </summary>
/// <remarks>
/// This list is the single registration point for providers. Почта России is served by
/// <see cref="PochtaRussiaTrackingService"/>, a scripted stand-in for its real HTTP API, while DHL and DPD
/// stay on <see cref="DummyTrackingService"/> until their integrations exist. Adding a real carrier later
/// means replacing one entry here; the grid dropdown and the settings dialog pick it up automatically.
/// </remarks>
public static class DummyTrackingServices
{
    /// <summary>The provider code of the DHL integration.</summary>
    public const string DhlCode = "DHL";

    /// <summary>The provider code of the DPD integration.</summary>
    public const string DpdCode = "DPD";

    /// <summary>
    /// Creates every provider, in the order they are offered to the operator.
    /// </summary>
    /// <param name="statusMappings">
    /// The configured status mapping used by the provider adapters; when omitted, the built-in defaults
    /// from <see cref="AppConfig.CreateDefault"/> are used.
    /// </param>
    /// <returns>The providers to register.</returns>
    public static IReadOnlyList<ITrackingService> CreateAll(StatusMappingCatalog? statusMappings = null) =>
    [
        new PochtaRussiaTrackingService(statusMappings ?? StatusMappingCatalog.Default),
        new DummyTrackingService(new TrackingServiceDescriptor(DhlCode, "DHL")),
        new DummyTrackingService(new TrackingServiceDescriptor(DpdCode, "DPD")),
    ];
}
