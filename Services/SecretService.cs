namespace MailIntegrator.Services;

/// <summary>
/// Identifies an external service whose credentials are kept in the operating system vault.
/// </summary>
public enum SecretService
{
    /// <summary>The Почта России tracking API.</summary>
    PochtaRussia,

    /// <summary>The 1C ERP integration endpoint.</summary>
    OneC,
}

/// <summary>
/// Maps <see cref="SecretService"/> values to their vault target names and display names.
/// </summary>
public static class SecretServices
{
    private const string TargetNamePrefix = "MailIntegrator/";

    /// <summary>
    /// Gets the Windows Credential Manager target name used for the Почта России API credentials.
    /// </summary>
    public const string PochtaRussiaTargetName = TargetNamePrefix + "PochtaRussia";

    /// <summary>
    /// Gets the Windows Credential Manager target name used for the 1C ERP credentials.
    /// </summary>
    public const string OneCTargetName = TargetNamePrefix + "OneC";

    /// <summary>
    /// Gets every supported service, which is also the order they are presented to the user.
    /// </summary>
    public static IReadOnlyList<SecretService> All { get; } = [SecretService.PochtaRussia, SecretService.OneC];

    /// <summary>
    /// Returns the vault target name for a service.
    /// </summary>
    /// <param name="service">The service to look up.</param>
    /// <returns>The credential target name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The service is not recognised.</exception>
    public static string ToTargetName(this SecretService service) => service switch
    {
        SecretService.PochtaRussia => PochtaRussiaTargetName,
        SecretService.OneC => OneCTargetName,
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown secret service."),
    };

    /// <summary>
    /// Returns the human readable name of a service, as shown in the settings dialog.
    /// </summary>
    /// <param name="service">The service to look up.</param>
    /// <returns>The display name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The service is not recognised.</exception>
    public static string ToDisplayName(this SecretService service) => service switch
    {
        SecretService.PochtaRussia => "Почта России API",
        SecretService.OneC => "1C ERP",
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown secret service."),
    };
}
