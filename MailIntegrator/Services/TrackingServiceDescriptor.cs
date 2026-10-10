namespace MailIntegrator.Services;

/// <summary>
/// Identifies a tracking provider for the operator interface and for credential lookup.
/// </summary>
/// <param name="Code">The stable code stored on a parcel and used as the credential key.</param>
/// <param name="DisplayName">The human readable name shown in the grid and settings dialog.</param>
public sealed record TrackingServiceDescriptor(string Code, string DisplayName)
{
    /// <summary>
    /// Builds the credential key that belongs to this provider.
    /// </summary>
    /// <returns>The credential target for this provider.</returns>
    public CredentialTarget ToCredentialTarget() => new(Code, DisplayName);
}
