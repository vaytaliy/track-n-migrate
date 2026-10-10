namespace MailIntegrator.Services;

/// <summary>
/// Resolves the tracking providers that are registered in the application.
/// </summary>
/// <remarks>
/// The registry is the single source of truth for "which providers exist": it drives the provider
/// dropdown of the parcel grid and the credential editors of the settings dialog.
/// </remarks>
public interface ITrackingServiceRegistry
{
    /// <summary>
    /// Gets every registered provider in presentation order.
    /// </summary>
    IReadOnlyList<ITrackingService> Services { get; }

    /// <summary>
    /// Finds the provider that owns a code.
    /// </summary>
    /// <param name="code">The provider code stored on a parcel.</param>
    /// <returns>The matching provider, or <see langword="null"/> when no provider uses the code.</returns>
    ITrackingService? FindByCode(string? code);

    /// <summary>
    /// Reports whether a code belongs to a registered provider.
    /// </summary>
    /// <param name="code">The candidate provider code.</param>
    /// <returns><see langword="true"/> when the code is registered.</returns>
    bool ContainsCode(string? code);
}
