namespace MailIntegrator.Services;

/// <summary>
/// Identifies an external service whose credentials are kept in the operating system vault.
/// </summary>
/// <param name="Code">The stable key used to build the vault target name.</param>
/// <param name="DisplayName">The human readable name shown in the settings dialog.</param>
/// <remarks>
/// A value object rather than an enum, so integrators can add providers without changing this assembly.
/// </remarks>
public sealed record CredentialTarget(string Code, string DisplayName);

/// <summary>
/// Credential targets that are not tied to a tracking provider.
/// </summary>
public static class CredentialTargets
{
    /// <summary>
    /// Gets the credential target of the 1C target system.
    /// </summary>
    public static CredentialTarget OneC { get; } = new("OneC", "1C ERP");
}
