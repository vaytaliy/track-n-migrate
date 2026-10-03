namespace MailIntegrator.Services;

/// <summary>
/// Builds the simulated tracking providers that ship with the application skeleton.
/// </summary>
/// <remarks>
/// This list is the single registration point for providers. Adding a real carrier later means replacing
/// one entry here; the grid dropdown and the settings dialog pick it up automatically.
/// </remarks>
public static class DummyTrackingServices
{
    /// <summary>The provider code of the Почта России integration.</summary>
    public const string PochtaRussiaCode = "PochtaRussia";

    /// <summary>The provider code of the DHL integration.</summary>
    public const string DhlCode = "DHL";

    /// <summary>The provider code of the DPD integration.</summary>
    public const string DpdCode = "DPD";

    /// <summary>
    /// Creates every simulated provider, in the order they are offered to the operator.
    /// </summary>
    /// <returns>The providers to register.</returns>
    public static IReadOnlyList<ITrackingService> CreateAll() =>
    [
        new DummyTrackingService(new TrackingServiceDescriptor(PochtaRussiaCode, "Почта России")),
        new DummyTrackingService(new TrackingServiceDescriptor(DhlCode, "DHL")),
        new DummyTrackingService(new TrackingServiceDescriptor(DpdCode, "DPD")),
    ];
}
